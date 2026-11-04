using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Auth;
using TiemChe.Contracts.Catalog;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class AccountService(ITiemCheData data, IPasswordHasher passwords, IResetEmailSender emailSender,
    TimeProvider time)
{
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(15);
    private const int ResetAttemptLimit = 5;

    public Task<UserProfile> RegisterAsync(RegisterRequest request, CancellationToken ct) =>
        CreateAsync(request, RoleName.Customer, ct);

    public Task<UserProfile> CreateStaffAsync(RegisterRequest request, CancellationToken ct) =>
        CreateAsync(request, RoleName.Staff, ct);

    public async Task BootstrapAdminAsync(RegisterRequest request, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        if (await data.Users.AnyAsync(x => x.Role.Name == RoleName.Admin, ct))
            throw Check.Conflict("Hệ thống đã có tài khoản quản trị.");
        await CreateAsync(request, RoleName.Admin, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<UserProfile> CreateAsync(RegisterRequest request, RoleName role, CancellationToken ct)
    {
        var email = Check.Email(request.Email);
        var normalizedEmail = email.ToUpperInvariant();
        var name = Check.Text(request.FullName, 120, "họ và tên");
        var phone = Check.Optional(request.Phone, 24, "số điện thoại");
        var password = Check.Password(request.Password);
        if (await data.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct))
            throw Check.Conflict("Email này đã được đăng ký.");

        var roleEntity = await data.Roles.SingleAsync(x => x.Name == role, ct);
        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = roleEntity,
            FullName = name,
            Phone = phone,
            PasswordHash = passwords.Hash(password)
        };
        data.Users.Add(user);
        await data.SaveChangesAsync(ct);
        return Profile(user);
    }

    public async Task<(UserProfile Profile, Guid Stamp)> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Check.Email(request.Email).ToUpperInvariant();
        if (!Check.CanVerifyPassword(request.Password))
            throw new BusinessException(401, "Email hoặc mật khẩu không đúng.");
        var user = await data.Users.AsNoTracking().Include(x => x.Role)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct);
        var verified = passwords.Verify(request.Password!, user?.PasswordHash
            ?? "$2a$12$KZ3CUirBnvekiX3tlKyvNeFZrqAnV3PusCSKXDaOfoQmpqa3k.OTe");
        if (!verified || user is null || !user.IsActive)
            throw new BusinessException(401, "Email hoặc mật khẩu không đúng.");
        return (Profile(user), user.SecurityStamp);
    }

    public async Task<bool> IsSessionValidAsync(int id, Guid stamp, CancellationToken ct) =>
        await data.Users.AnyAsync(x => x.Id == id && x.IsActive && x.SecurityStamp == stamp, ct);

    public async Task<UserProfile> GetAsync(int id, CancellationToken ct) => Profile(await FindAsync(id, ct));

    public async Task<UserProfile> UpdateAsync(int id, ProfileRequest request, CancellationToken ct)
    {
        var user = await FindAsync(id, ct);
        user.FullName = Check.Text(request.FullName, 120, "họ và tên");
        user.Phone = Check.Optional(request.Phone, 24, "số điện thoại");
        await data.SaveChangesAsync(ct);
        return Profile(user);
    }

    public async Task ChangePasswordAsync(int id, ChangePasswordRequest request, CancellationToken ct)
    {
        var next = Check.Password(request.NewPassword);
        var user = await FindAsync(id, ct);
        if (!Check.CanVerifyPassword(request.CurrentPassword) ||
            !passwords.Verify(request.CurrentPassword!, user.PasswordHash))
            throw Check.Invalid("Mật khẩu hiện tại không đúng.");
        var updatedHash = passwords.Hash(next);
        var nextStamp = Guid.NewGuid();
        await using var transaction = await data.BeginWriteAsync(ct);
        var updated = await data.Users.Where(x => x.Id == id && x.SecurityStamp == user.SecurityStamp && x.IsActive)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.PasswordHash, updatedHash)
                .SetProperty(x => x.SecurityStamp, nextStamp), ct);
        if (updated == 0)
            throw Check.Conflict("Tài khoản đã thay đổi. Vui lòng đăng nhập lại.");
        await InvalidateCodesAsync(id, time.GetUtcNow().UtcDateTime, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RequestResetAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var email = Check.Email(request.Email).ToUpperInvariant();
        if (!emailSender.IsConfigured)
            throw new BusinessException(503, "Chức năng gửi email chưa được cấu hình.");

        var now = time.GetUtcNow().UtcDateTime;
        await using var transaction = await data.BeginWriteAsync(ct);
        var user = await data.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == email && x.IsActive, ct);
        if (user is null || await data.PasswordResetRequests.AnyAsync(
            x => x.UserId == user.Id && x.ExpiresAtUtc > now.Add(ResetLifetime).AddMinutes(-1), ct))
            return;
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        await InvalidateCodesAsync(user.Id, now, ct);
        data.PasswordResetRequests.Add(new PasswordResetRequest
        {
            UserId = user.Id,
            CodeHash = CodeHash(code),
            ExpiresAtUtc = now.Add(ResetLifetime)
        });
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await emailSender.SendAsync(user.Email, code, ct);
    }

    public async Task ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var email = Check.Email(request.Email).ToUpperInvariant();
        var code = Check.Text(request.Code, 24, "mã đặt lại").ToUpperInvariant();
        var next = Check.Password(request.NewPassword);
        var now = time.GetUtcNow().UtcDateTime;
        await using var transaction = await data.BeginWriteAsync(ct);
        var reset = await data.PasswordResetRequests.Include(x => x.User)
            .Where(x => x.User.NormalizedEmail == email && x.User.IsActive && x.ExpiresAtUtc > now
                && x.UsedAtUtc == null && x.AttemptCount < ResetAttemptLimit)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (reset is null)
            throw Check.Invalid("Mã đặt lại không hợp lệ hoặc đã hết hạn.");

        reset.AttemptCount++;
        var valid = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reset.CodeHash),
            Convert.FromHexString(CodeHash(code)));
        if (valid)
        {
            reset.UsedAtUtc = now;
            reset.User.PasswordHash = passwords.Hash(next);
            reset.User.SecurityStamp = Guid.NewGuid();
        }
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (!valid)
            throw Check.Invalid("Mã đặt lại không hợp lệ hoặc đã hết hạn.");
    }

    public async Task<PageResult<UserProfile>> ListAsync(RoleName role, int? page, int? pageSize,
        string? search, CancellationToken ct)
    {
        var pagination = Check.Page(page, pageSize);
        var term = Check.Optional(search, 120, "từ khóa");
        var query = data.Users.AsNoTracking().Where(x => x.Role.Name == role);
        if (term is not null)
        {
            var normalized = term.ToUpperInvariant();
            var normalizedName = VietnameseSearch.Normalize(term);
            query = query.Where(x => x.NormalizedEmail.Contains(normalized)
                || VietnameseSearch.Contains(x.FullName, normalizedName));
        }
        var count = await query.CountAsync(ct);
        var users = await query.Include(x => x.Role).OrderBy(x => x.Id)
            .Skip((pagination.Page - 1) * pagination.Size).Take(pagination.Size).ToListAsync(ct);
        return new PageResult<UserProfile>(users.Select(Profile).ToArray(), count, pagination.Page, pagination.Size);
    }

    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        await using var transaction = await data.BeginWriteAsync(ct);
        var user = await FindAsync(id, ct);
        if (user.Role.Name == RoleName.Admin)
            throw Check.Invalid("Không thể khóa tài khoản quản trị ở màn hình này.");
        if (user.IsActive != active)
        {
            user.IsActive = active;
            user.SecurityStamp = Guid.NewGuid();
            await InvalidateCodesAsync(id, time.GetUtcNow().UtcDateTime, ct);
            await data.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    private async Task<User> FindAsync(int id, CancellationToken ct) =>
        await data.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw Check.Missing("Không tìm thấy tài khoản.");

    private Task<int> InvalidateCodesAsync(int userId, DateTime now, CancellationToken ct) =>
        data.PasswordResetRequests.Where(x => x.UserId == userId && x.UsedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.UsedAtUtc, now), ct);

    private static UserProfile Profile(User user) =>
        new(user.Id, user.Email, user.FullName, user.Phone, user.Role.Name, user.IsActive);

    private static string CodeHash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
