namespace WorshipProjectionApp.Models;

public class BibleVerse
{
    public int BookNumber { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = string.Empty;

    public string Reference => $"{BookName} {Chapter}:{Verse}";

    public SlideItem ToSlideItem()
    {
        return new SlideItem
        {
            Header = Reference,
            Content = $"\"{Text.Trim()}\"\n\n— {Reference}"
        };
    }
}