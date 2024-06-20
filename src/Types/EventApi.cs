namespace AppointmentScheduler.Types;

public class EventApi
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public bool? AllDay { get; set; }
    public Dictionary<string, object>? ExtendedProps { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BorderColor { get; set; }
}