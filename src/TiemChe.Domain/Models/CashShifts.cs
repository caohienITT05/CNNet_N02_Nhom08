namespace TiemChe.Domain.Models;

public sealed class CashShift
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime OpenedAtUtc { get; set; }
    public long OpeningCashVnd { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public long? CountedCashVnd { get; set; }
    public long? ExpectedCashVnd { get; set; }
    public long? DifferenceVnd { get; set; }
    public string? Note { get; set; }
}
