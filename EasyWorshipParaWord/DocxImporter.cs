using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EasyWorshipParaWord;

/// <summary>
/// Lê um .docx com a estrutura criada por esta aplicação (e editado no Word):
///   Título 1            = início de uma música
///   linha cinzenta      = autor/copyright (opcional, ignorada na importação)
///   parágrafo a negrito = etiqueta de um slide (Verse, Chorus, End...)
///   restantes parágrafos= linhas da letra desse slide
/// Um parágrafo vazio dentro de um slide começa um novo slide.
/// </summary>
public static class DocxImporter
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private sealed class SlideB
    {
        public string Label = "";
        public List<string> Lines = new();
    }

    public static List<Song> Read(string docxPath)
    {
        XDocument doc;
        XDocument? styles = null;
        using (var fs = new FileStream(docxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            ZipArchiveEntry docEntry = zip.GetEntry("word/document.xml")
                ?? throw new InvalidDataException("Este ficheiro não parece ser um documento Word (.docx) válido.");
            using (Stream s1 = docEntry.Open()) doc = XDocument.Load(s1);

            ZipArchiveEntry? stylesEntry = zip.GetEntry("word/styles.xml");
            if (stylesEntry != null)
            {
                using Stream s2 = stylesEntry.Open();
                styles = XDocument.Load(s2);
            }
        }

        XElement body = doc.Root?.Element(W + "body")
            ?? throw new InvalidDataException("O documento Word está vazio ou danificado.");

        // Identifica o(s) estilo(s) "Título 1" (o id pode variar conforme o idioma do Word).
        var headingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Heading1", "Ttulo1" };
        if (styles?.Root != null)
        {
            foreach (XElement st in styles.Root.Elements(W + "style"))
            {
                string? id = (string?)st.Attribute(W + "styleId");
                string? name = (string?)st.Element(W + "name")?.Attribute(W + "val");
                if (id != null && name != null &&
                    (name.Equals("heading 1", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("título 1", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("titulo 1", StringComparison.OrdinalIgnoreCase)))
                {
                    headingIds.Add(id);
                }
            }
        }

        var songs = new List<Song>();
        string? title = null;
        string meta = "";
        var slides = new List<SlideB>();
        SlideB? cur = null;
        bool expectMeta = false;
        bool breakPending = false;

        void Finish()
        {
            if (title != null)
            {
                var list = slides.Select(sl => new Slide(sl.Label, string.Join("\n", sl.Lines))).ToList();
                songs.Add(new Song(0, title, meta, "", "", "", list));
            }
            title = null;
            meta = "";
            slides = new List<SlideB>();
            cur = null;
            expectMeta = false;
            breakPending = false;
        }

        foreach (XElement p in body.Descendants(W + "p"))
        {
            XElement? pPr = p.Element(W + "pPr");
            string? style = (string?)pPr?.Element(W + "pStyle")?.Attribute(W + "val");
            bool outlineZero = (string?)pPr?.Element(W + "outlineLvl")?.Attribute(W + "val") == "0";
            string text = GetText(p);

            if ((style != null && headingIds.Contains(style)) || outlineZero)
            {
                Finish();
                string t = text.Replace('\n', ' ').Trim();
                title = t.Length == 0 ? "(sem título)" : t;
                expectMeta = true;
                continue;
            }

            if (title == null) continue; // título do documento, subtítulo, etc.

            string trimmed = text.Trim();

            if (expectMeta)
            {
                expectMeta = false;
                if (trimmed.Length > 0 && IsMeta(p, trimmed))
                {
                    meta = trimmed;
                    continue;
                }
            }

            if (trimmed.Length == 0)
            {
                breakPending = true;
                continue;
            }

            if (AllTextRunsBold(p))
            {
                cur = new SlideB { Label = trimmed.Replace('\n', ' ') };
                slides.Add(cur);
                breakPending = false;
                continue;
            }

            if (cur == null || (breakPending && cur.Lines.Count > 0))
            {
                cur = new SlideB();
                slides.Add(cur);
            }
            breakPending = false;

            foreach (string line in trimmed.Split('\n'))
            {
                string l = line.Trim();
                if (l.Length > 0) cur.Lines.Add(l);
            }
        }
        Finish();

        return songs;
    }

    private static string GetText(XElement p)
    {
        var sb = new StringBuilder();
        foreach (XElement e in p.Descendants())
        {
            if (e.Name == W + "t")
            {
                sb.Append(e.Value);
            }
            else if (e.Name == W + "tab" && e.Parent?.Name == W + "r")
            {
                sb.Append('\t');
            }
            else if (e.Name == W + "br" || e.Name == W + "cr")
            {
                string? type = (string?)e.Attribute(W + "type");
                if (type == null || type == "textWrapping") sb.Append('\n');
            }
        }
        return sb.ToString().Replace(' ', ' ');
    }

    private static IEnumerable<XElement> TextRuns(XElement p) =>
        p.Descendants(W + "r").Where(r => r.Descendants(W + "t").Any(t => t.Value.Trim().Length > 0));

    private static bool AllTextRunsBold(XElement p)
    {
        bool any = false;
        foreach (XElement r in TextRuns(p))
        {
            any = true;
            XElement? b = r.Element(W + "rPr")?.Element(W + "b");
            if (b == null) return false;
            string? v = (string?)b.Attribute(W + "val");
            if (v == "0" || v == "false" || v == "off") return false;
        }
        return any;
    }

    private static bool IsMeta(XElement p, string trimmedText)
    {
        if (trimmedText.Contains("  ·  ")) return true;
        bool any = false;
        foreach (XElement r in TextRuns(p))
        {
            any = true;
            string? color = (string?)r.Element(W + "rPr")?.Element(W + "color")?.Attribute(W + "val");
            if (!string.Equals(color, "777777", StringComparison.OrdinalIgnoreCase)) return false;
        }
        return any;
    }
}
