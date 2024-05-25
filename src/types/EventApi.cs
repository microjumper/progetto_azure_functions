namespace appointment_scheduler.types;

public class EventApi
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? StartStr { get; set; }
    public string? EndStr { get; set; }
    public bool? AllDay { get; set; }
    public Dictionary<string, object>? ExtendedProps { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string? GroupId { get; set; }
    public string? Url { get; set; }
    public string? Display { get; set; }
    public bool? Editable { get; set; }
    public bool? StartEditable { get; set; }
    public bool? DurationEditable { get; set; }
    public bool? ResourceEditable { get; set; }
    public bool? Overlap { get; set; }
    public string? Constraint { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BorderColor { get; set; }
    public string? TextColor { get; set; }
    public string? ClassNames { get; set; }
}