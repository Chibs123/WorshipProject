using System.Text.RegularExpressions;

namespace WorshipProjectionApp.Models;

public class Song
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Artist { get; set; }
    public string RawLyrics { get; set; } = string.Empty;

    public List<SlideItem> ParseSlides()
    {
        var slides = new List<SlideItem>();
        if (string.IsNullOrWhiteSpace(RawLyrics)) return slides;

        // Normaliza quebras de linha e separa por blocos de linha dupla
        var blocks = Regex.Split(RawLyrics.Trim(), @"(?:\r?\n){2,}");

        int index = 1;
        foreach (var block in blocks)
        {
            var lines = block.Trim().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0) continue;

            string header;
            string content;

            // Se a primeira linha tiver formato de tag [Coro], [Verso], usa-a como cabeçalho
            if (lines[0].Trim().StartsWith("[") && lines[0].Trim().EndsWith("]"))
            {
                header = lines[0].Trim().Trim('[', ']');
                content = string.Join(Environment.NewLine, lines.Skip(1)).Trim();
            }
            else
            {
                header = $"Parte {index++}";
                content = block.Trim();
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                slides.Add(new SlideItem { Header = header, Content = content });
            }
        }

        return slides;
    }
}