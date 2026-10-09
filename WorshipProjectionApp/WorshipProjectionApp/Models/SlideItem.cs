namespace WorshipProjectionApp.Models;

public class SlideItem
{
    public string Header { get; set; } = string.Empty; // Ex: "Verso 1", "Refrão", "João 3:16"
    public string Content { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Header) ? Content : $"[{Header}]\n{Content}";
}