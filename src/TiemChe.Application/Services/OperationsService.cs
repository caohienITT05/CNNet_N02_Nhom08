using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Operations;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class OperationsService(ITiemCheData data, TimeProvider time, CheckoutSettings checkout)
{
    public StaffCheckoutOptions CheckoutOptions() =>
        new(checkout.AllowsBankTransfer, checkout.SupportsQr, checkout.BankName);

    public async Task<IReadOnlyList<StaffCustomerDto>> CustomersAsync(string? search, CancellationToken ct)
    {
        var term = Check.Optional(search, 120, "từ khóa");
        var customers = data.Users.AsNoTracking().Where(x => x.Role.Name == RoleName.Customer
            && x.IsActive && x.NormalizedEmail != "WALKIN@TIEMCHE.LOCAL");
        if (term is not null)
        {
            var normalizedName = VietnameseSearch.Normalize(term);
            var email = term.ToUpperInvariant();
            customers = customers.Where(x => VietnameseSearch.Contains(x.FullName, normalizedName)
                || x.NormalizedEmail.Contains(email) || (x.Phone != null && x.Phone.Contains(term)));
        }
        return await customers.OrderBy(x => x.FullName).ThenBy(x => x.Id).Take(10)
            .Select(x => new StaffCustomerDto(x.Id, x.FullName, x.Phone, x.Email)).ToListAsync(ct);
    }

    public async Task<ShiftSummary> SummaryAsync(int actorId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime.AddHours(7));
        var start = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var end = start.AddDays(1);
        var orders = data.Orders.AsNoTracking().Where(x => x.PlacedAtUtc >= start && x.PlacedAtUtc < end);
        var counts = await orders.GroupBy(x => x.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() }).ToListAsync(ct);
        var byStatus = counts.ToDictionary(x => x.Status, x => x.Count);
        var statuses = Enum.GetValues<OrderStatus>()
            .Select(status => new ShiftStatusCount(status, byStatus.GetValueOrDefault(status))).ToArray();
        var pending = await orders.CountAsync(x => x.Status == OrderStatus.Pending, ct);
        var pendingUnpaid = await orders.CountAsync(x =>
            x.Status == OrderStatus.Pending && x.Payment.Status == PaymentStatus.Unpaid, ct);
        var cash = await data.Payments.AsNoTracking().Where(x => x.Method == PaymentMethod.CashOnDelivery
                && x.Status == PaymentStatus.Paid && x.ConfirmedAtUtc >= start && x.ConfirmedAtUtc < end)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var transfer = await data.Payments.AsNoTracking().Where(x => x.Method == PaymentMethod.BankTransfer
                && x.Status == PaymentStatus.Paid && x.ConfirmedAtUtc >= start && x.ConfirmedAtUtc < end)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var mine = await data.OrderStatusHistories.AsNoTracking().Where(x => x.ChangedByUserId == actorId
                && x.ChangedAtUtc >= start && x.ChangedAtUtc < end
                && (x.NewStatus == OrderStatus.Preparing || x.NewStatus == OrderStatus.Completed))
            .Select(x => x.OrderId).Distinct().CountAsync(ct);
        return new ShiftSummary(today, statuses, pending, pendingUnpaid, cash, transfer, mine);
    }

    public async Task<CashShiftDto?> CurrentShiftAsync(int actorId, CancellationToken ct)
    {
        var shift = await data.CashShifts.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.UserId == actorId && x.ClosedAtUtc == null, ct);
        return shift is null ? null : await DescribeShiftAsync(shift, time.GetUtcNow().UtcDateTime, ct);
    }

    public async Task<CashShiftDto> OpenShiftAsync(int actorId, long openingCashVnd, CancellationToken ct)
    {
        if (openingCashVnd is < 0 or > Check.MaxPrice)
            throw Check.Invalid("Tiền đầu ca phải từ 0 đến giới hạn cho phép.");
        await using var transaction = await data.BeginWriteAsync(ct);
        if (await data.CashShifts.AnyAsync(x => x.UserId == actorId && x.ClosedAtUtc == null, ct))
            throw Check.Conflict("Bạn đang có một ca chưa đóng.");
        var shift = new CashShift
        {
            UserId = actorId,
            OpenedAtUtc = time.GetUtcNow().UtcDateTime,
            OpeningCashVnd = openingCashVnd
        };
        data.CashShifts.Add(shift);
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        shift = await data.CashShifts.AsNoTracking().Include(x => x.User)
            .SingleAsync(x => x.Id == shift.Id, ct);
        return await DescribeShiftAsync(shift, shift.OpenedAtUtc, ct);
    }

    public async Task<CashShiftDto> CloseShiftAsync(int actorId, long countedCashVnd, string? rawNote,
        CancellationToken ct)
    {
        if (countedCashVnd is < 0 or > Check.MaxPrice)
            throw Check.Invalid("Tiền kiểm đếm phải từ 0 đến giới hạn cho phép.");
        var note = Check.Optional(rawNote, 500, "ghi chú");
        await using var transaction = await data.BeginWriteAsync(ct);
        var shift = await data.CashShifts.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.UserId == actorId && x.ClosedAtUtc == null, ct)
            ?? throw Check.Missing("Bạn chưa mở ca làm việc.");
        var now = time.GetUtcNow().UtcDateTime;
        var (cash, transfer, handled) = await ShiftPaymentsAsync(actorId, shift.OpenedAtUtc, now, ct);
        var expected = checked(shift.OpeningCashVnd + cash);
        if (expected > Check.MaxPrice)
            throw Check.Invalid("Tổng tiền dự kiến vượt giới hạn cho phép.");
        var difference = countedCashVnd - expected;
        if (difference != 0 && note is null)
            throw Check.Invalid("Vui lòng ghi chú khi tiền kiểm đếm có chênh lệch.");
        shift.ClosedAtUtc = now;
        shift.CountedCashVnd = countedCashVnd;
        shift.ExpectedCashVnd = expected;
        shift.DifferenceVnd = difference;
        shift.Note = note;
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new CashShiftDto(shift.Id, shift.UserId, shift.User.FullName, shift.OpenedAtUtc,
            shift.OpeningCashVnd, shift.ClosedAtUtc, shift.CountedCashVnd, shift.ExpectedCashVnd,
            shift.DifferenceVnd, shift.Note, cash, transfer, handled);
    }

    public async Task<PageResult<CashShiftDto>> ShiftHistoryAsync(int? actorId, int? staffId, int? page,
        int? pageSize, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var query = data.CashShifts.AsNoTracking().Include(x => x.User).AsQueryable();
        if (actorId.HasValue)
            query = query.Where(x => x.UserId == actorId.Value);
        else if (staffId.HasValue)
            query = query.Where(x => x.UserId == staffId.Value);
        var count = await query.CountAsync(ct);
        var shifts = await query.OrderByDescending(x => x.OpenedAtUtc).ThenByDescending(x => x.Id)
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size).ToListAsync(ct);
        var result = new List<CashShiftDto>(shifts.Count);
        foreach (var shift in shifts)
            result.Add(await DescribeShiftAsync(shift, shift.ClosedAtUtc ?? time.GetUtcNow().UtcDateTime, ct));
        return new PageResult<CashShiftDto>(result, count, pagination.Page, pagination.Size);
    }

    public async Task<AvailabilityCatalog> AvailabilityAsync(CancellationToken ct)
    {
        var products = await data.Products.AsNoTracking()
            .OrderBy(x => x.Category.DisplayOrder).ThenBy(x => x.CategoryId).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new AvailabilityProduct(x.Id, x.Name, x.CategoryId, x.Category.Name, x.IsActive,
                x.Sizes.OrderBy(size => size.PriceVnd).ThenBy(size => size.Id)
                    .Select(size => new AvailabilitySize(size.Id, size.Name, size.PriceVnd, size.IsActive)).ToArray()))
            .ToListAsync(ct);
        var toppings = await data.Toppings.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new ToppingDto(x.Id, x.Name, x.PriceVnd, x.ImagePath, x.IsActive)).ToListAsync(ct);
        return new AvailabilityCatalog(products, toppings);
    }

    public async Task SetProductAvailabilityAsync(int id, bool isActive, int actorId, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var current = await data.Products.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Name, x.IsActive }).SingleOrDefaultAsync(ct)
            ?? throw Check.Missing("Không tìm thấy món.");
        if (current.IsActive == isActive)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (await data.Products.Where(x => x.Id == id && x.IsActive == current.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, isActive), ct) == 0)
            throw Check.Conflict("Tình trạng món đã thay đổi. Vui lòng tải lại.");
        data.AvailabilityLogs.Add(new AvailabilityLog
        {
            EntityType = AvailabilityEntityType.Product,
            EntityId = id,
            EntityName = current.Name,
            OldValue = current.IsActive,
            NewValue = isActive,
            ChangedByUserId = actorId,
            ChangedAtUtc = time.GetUtcNow().UtcDateTime
        });
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SetProductSizeAvailabilityAsync(int id, bool isActive, int actorId, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var current = await data.ProductSizes.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { Name = x.Product.Name + " (" + x.Name + ")", x.IsActive })
            .SingleOrDefaultAsync(ct)
            ?? throw Check.Missing("Không tìm thấy size.");
        if (current.IsActive == isActive)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (await data.ProductSizes.Where(x => x.Id == id && x.IsActive == current.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, isActive), ct) == 0)
            throw Check.Conflict("Tình trạng size đã thay đổi. Vui lòng tải lại.");
        data.AvailabilityLogs.Add(new AvailabilityLog
        {
            EntityType = AvailabilityEntityType.ProductSize,
            EntityId = id,
            EntityName = current.Name,
            OldValue = current.IsActive,
            NewValue = isActive,
            ChangedByUserId = actorId,
            ChangedAtUtc = time.GetUtcNow().UtcDateTime
        });
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task SetToppingAvailabilityAsync(int id, bool isActive, int actorId, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var current = await data.Toppings.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Name, x.IsActive }).SingleOrDefaultAsync(ct)
            ?? throw Check.Missing("Không tìm thấy topping.");
        if (current.IsActive == isActive)
        {
            await transaction.CommitAsync(ct);
            return;
        }
        if (await data.Toppings.Where(x => x.Id == id && x.IsActive == current.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, isActive), ct) == 0)
            throw Check.Conflict("Tình trạng topping đã thay đổi. Vui lòng tải lại.");
        data.AvailabilityLogs.Add(new AvailabilityLog
        {
            EntityType = AvailabilityEntityType.Topping,
            EntityId = id,
            EntityName = current.Name,
            OldValue = current.IsActive,
            NewValue = isActive,
            ChangedByUserId = actorId,
            ChangedAtUtc = time.GetUtcNow().UtcDateTime
        });
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<PageResult<AvailabilityLogDto>> AvailabilityLogsAsync(int? page, CancellationToken ct)
    {
        var pagination = Check.Page(page, 50);
        var query = data.AvailabilityLogs.AsNoTracking();
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.ChangedAtUtc).ThenByDescending(x => x.Id)
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size)
            .Select(x => new AvailabilityLogDto(x.Id, x.EntityType, x.EntityId, x.EntityName, x.OldValue,
                x.NewValue, x.ChangedByUserId, x.ChangedByUser.FullName, x.ChangedAtUtc)).ToListAsync(ct);
        return new PageResult<AvailabilityLogDto>(rows, count, pagination.Page, pagination.Size);
    }

    private async Task<CashShiftDto> DescribeShiftAsync(CashShift shift, DateTime endUtc, CancellationToken ct)
    {
        var (cash, transfer, handled) = await ShiftPaymentsAsync(shift.UserId, shift.OpenedAtUtc, endUtc, ct);
        var expected = shift.ExpectedCashVnd ?? checked(shift.OpeningCashVnd + cash);
        return new CashShiftDto(shift.Id, shift.UserId, shift.User.FullName, shift.OpenedAtUtc,
            shift.OpeningCashVnd, shift.ClosedAtUtc, shift.CountedCashVnd, expected, shift.DifferenceVnd,
            shift.Note, cash, transfer, handled);
    }

    private async Task<(long Cash, long Transfer, int Handled)> ShiftPaymentsAsync(int actorId,
        DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var payments = data.Payments.AsNoTracking().Where(x => x.ConfirmedByUserId == actorId
            && x.ConfirmedAtUtc >= startUtc && x.ConfirmedAtUtc <= endUtc);
        var paid = payments.Where(x => x.Status == PaymentStatus.Paid);
        var cash = await paid.Where(x => x.Method == PaymentMethod.CashOnDelivery)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var transfer = await paid.Where(x => x.Method == PaymentMethod.BankTransfer)
            .SumAsync(x => (long?)x.AmountVnd, ct) ?? 0;
        var handled = await paid.Select(x => x.OrderId).Distinct().CountAsync(ct);
        return (cash, transfer, handled);
    }
}
