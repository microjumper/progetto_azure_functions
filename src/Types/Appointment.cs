namespace AppointmentScheduler.Types;
public class Appointment
{
    public string? Id { get; set;}
    public required string LegalServiceId { get; set; }
    public required string LegalServiceTitle { get; set; }
    public string? EventId { get; set; }
    public string? EventDate { get; set; }
    public required User User { get; set; }
    public List<FileMetadata> FileMetadata { get; set; } = [];
}

public class FileMetadata
{
    public string? OriginalFileName { get; set; }
    public string? FileUrl { get; set; }
    public required User User { get; set; }
    public string? SasToken { get; set; }
}