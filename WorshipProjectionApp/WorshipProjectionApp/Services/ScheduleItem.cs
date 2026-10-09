namespace WorshipProjectionApp.Models;

public class ScheduleItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public List<SlideItem> Slides { get; set; } = new();

    public override string ToString() => Title;
}