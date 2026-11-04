using TiemChe.Domain.Enums;

namespace TiemChe.Domain.Models;

public sealed class AvailabilityLog
{
    public int Id { get; set; }
    public AvailabilityEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public required string EntityName { get; set; }
    public bool OldValue { get; set; }
    public bool NewValue { get; set; }
    public int ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public DateTime ChangedAtUtc { get; set; }
}
