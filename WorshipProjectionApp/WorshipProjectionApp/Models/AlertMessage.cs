namespace WorshipProjectionApp.Models;

public class AlertMessage
{
    public string Text { get; set; } = string.Empty;
    public bool ShowClock { get; set; }
    public bool ShowTimer { get; set; }
    public int TimerMinutes { get; set; } = 30;
}