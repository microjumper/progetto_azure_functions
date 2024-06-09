namespace AppointmentScheduler.Types;
public class Appointment
{
    public string? Id { get; set;}
    public string? LegalServiceId { get; set; }
    public string? LegalServiceTitle { get; set; }
    public string? EventId { get; set; }
    public string? EventDate { get; set; }
    public string? AccountId { get; set; }
    public List<FileMetadata> FileMetadata { get; set; } = [];
}

public class FileMetadata
{
    public string? OriginalFileName { get; set; }
    public string? FileUrl { get; set; }
    public string? AccountId { get; set; }
    public string? AccountEmail { get; set; }
}