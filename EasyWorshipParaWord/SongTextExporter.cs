using System.Text;

namespace EasyWorshipParaWord;

/// <summary>
/// Escreve um ficheiro .txt por música, para importar no EasyWorship (editor da música → ícone Importar).
/// Formato: etiqueta numa linha, letra por baixo, linha em branco entre slides.
/// </summary>
public static class SongTextExporter
{
    public static int Export(IEnumerable<Song> songs, string folder)
    {
        Directory.CreateDirectory(folder);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        foreach (Song s in songs)
        {
            string name = UniqueName(SafeFileName(s.Title), used);
            File.WriteAllText(Path.Combine(folder, name + ".txt"), Format(s), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            count++;
        }
        return count;
    }

    public static string Format(Song s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Slides.Count; i++)
        {
            Slide sl = s.Slides[i];
            if (sl.Label.Length > 0) sb.Append(sl.Label).Append("\r\n");
            foreach (string line in sl.Text.Split('\n'))
            {
                string l = line.Trim();
                if (l.Length > 0) sb.Append(l).Append("\r\n");
            }
            if (i < s.Slides.Count - 1) sb.Append("\r\n");
        }
        return sb.ToString();
    }

    private static string SafeFileName(string title)
    {
        var sb = new StringBuilder();
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in title.Trim())
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        string name = sb.ToString().TrimEnd('.', ' ');
        if (name.Length > 120) name = name.Substring(0, 120).TrimEnd('.', ' ');
        return name.Length == 0 ? "musica" : name;
    }

    private static string UniqueName(string baseName, HashSet<string> used)
    {
        string name = baseName;
        int n = 2;
        while (!used.Add(name))
        {
            name = $"{baseName} ({n++})";
        }
        return name;
    }
}
