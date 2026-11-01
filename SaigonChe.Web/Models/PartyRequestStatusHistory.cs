using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models;

public sealed class PartyRequestStatusHistory
{
    public int Id { get; set; }
    public int PartyRequestId { get; set; }
    public PartyRequest? PartyRequest { get; set; }
    public PartyRequestStatus? PreviousStatus { get; set; }
    public PartyRequestStatus Status { get; set; }
    public string? ActorId { get; set; }
    public ApplicationUser? Actor { get; set; }
    [StringLength(500)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
