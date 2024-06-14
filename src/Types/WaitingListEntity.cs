namespace AppointmentScheduler.Types;

public class WaitingListEntity
{
    public string? Id { get; set; }
    public required Appointment Appointment { get; set; }
    public string? AddedOn { get; set; }
}