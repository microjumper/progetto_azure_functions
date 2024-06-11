namespace AppointmentScheduler.Types;

public class WaitingListEntity
{
    public string? Id { get; set; }
    public required string LegalServiceId { get; set; }
    public required User User { get; set; }
    public string? JoinedAt { get; set; }
}