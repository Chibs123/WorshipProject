using System.Text.RegularExpressions;

namespace EasyWorshipParaWord;

public sealed record DiffResult(
    List<Song> ToExport,
    int Changed,
    int Added,
    int Unchanged,
    List<string> MissingInDocument);

/// <summary>Compara as músicas do Word editado com as das bases de dados originais.</summary>
public static class SongDiff
{
    public static DiffResult Compare(IReadOnlyList<Song> edited, IReadOnlyList<Song> original)
    {
        // Emparelha por título (se houver títulos repetidos, emparelha pela ordem).
        var byTitle = new Dictionary<string, Queue<Song>>(StringComparer.OrdinalIgnoreCase);
        foreach (Song s in original)
        {
            string key = s.Title.Trim();
            if (!byTitle.TryGetValue(key, out Queue<Song>? queue))
            {
                queue = new Queue<Song>();
                byTitle[key] = queue;
            }
            queue.Enqueue(s);
        }

        var toExport = new List<Song>();
        int changed = 0, added = 0, unchanged = 0;

        foreach (Song e in edited)
        {
            string key = e.Title.Trim();
            if (byTitle.TryGetValue(key, out Queue<Song>? match) && match.Count > 0)
            {
                Song o = match.Dequeue();
                if (Signature(o) == Signature(e))
                {
                    unchanged++;
                }
                else
                {
                    changed++;
                    toExport.Add(e);
                }
            }
            else
            {
                added++;
                toExport.Add(e);
            }
        }

        var missing = byTitle.Values
            .SelectMany(remaining => remaining)
            .Select(s => s.Title)
            .OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new DiffResult(toExport, changed, added, unchanged, missing);
    }

    /// <summary>
    /// Assinatura normalizada de uma música: etiquetas e linhas sem espaços a mais.
    /// Um slide sem etiqueta conta como continuação do slide anterior (no Word não há separador visível
    /// entre eles, por isso ao ler o documento as linhas ficam juntas).
    /// </summary>
    private static string Signature(Song s)
    {
        var parts = new List<string>();

        foreach (Slide sl in s.Slides)
        {
            string label = Clean(sl.Label);
            string text = string.Join("\n", sl.Text.Split('\n').Select(Clean).Where(l => l.Length > 0));

            if (label.Length == 0 && parts.Count > 0)
            {
                if (text.Length == 0) continue;
                string last = parts[^1];
                parts[^1] = last.EndsWith('\u0002') ? last + text : last + "\n" + text;
            }
            else
            {
                parts.Add(label + "\u0002" + text);
            }
        }

        return string.Join("\u0001", parts);
    }

    private static string Clean(string t) => Regex.Replace(t.Replace(' ', ' ').Trim(), @"\s+", " ");
}
