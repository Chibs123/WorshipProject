using System.IO.Compression;
using System.Text;

namespace EasyWorshipParaWord;

/// <summary>
/// Escreve um .docx (Office Open XML) sem dependências externas: um ZIP com alguns ficheiros XML.
/// Estrutura: cada música = Título 1 (aparece no painel de navegação do Word), linha com autor/copyright,
/// e depois os slides, cada um com a etiqueta (Verse, Chorus...) a negrito azul e a letra por baixo.
/// </summary>
public static class DocxExporter
{
    // ---- Modelos XML (constantes; o conteúdo começa na coluna 0 de propósito) ----

    private const string ContentTypes = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
<Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
</Types>
""";

    private const string RootRels = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>
""";

    private const string DocRels = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
</Relationships>
""";

    private const string Styles = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:docDefaults>
<w:rPrDefault><w:rPr><w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:eastAsia="Arial" w:cs="Arial"/><w:sz w:val="22"/><w:szCs w:val="22"/><w:lang w:val="pt-PT" w:eastAsia="en-US" w:bidi="ar-SA"/></w:rPr></w:rPrDefault>
<w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr></w:pPrDefault>
</w:docDefaults>
<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>
<w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:spacing w:after="120"/></w:pPr><w:rPr><w:b/><w:sz w:val="40"/><w:szCs w:val="40"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/><w:pPr><w:keepNext/><w:keepLines/><w:pBdr><w:bottom w:val="single" w:sz="6" w:space="2" w:color="BBBBBB"/></w:pBdr><w:spacing w:before="360" w:after="60"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:b/><w:sz w:val="28"/><w:szCs w:val="28"/></w:rPr></w:style>
</w:styles>
""";

    private const string Footer = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:jc w:val="center"/></w:pPr><w:r><w:rPr><w:color w:val="888888"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:rPr><w:color w:val="888888"/><w:sz w:val="18"/></w:rPr><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r><w:r><w:rPr><w:color w:val="888888"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:rPr><w:color w:val="888888"/><w:sz w:val="18"/></w:rPr><w:t>1</w:t></w:r><w:r><w:rPr><w:color w:val="888888"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="end"/></w:r></w:p></w:ftr>
""";

    private const string DocStart = """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>
""";

    private const string DocEnd = """
<w:sectPr><w:footerReference w:type="default" r:id="rId2"/><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="567" w:footer="567" w:gutter="0"/></w:sectPr></w:body></w:document>
""";

    // ---- Formatação direta dos parágrafos ----

    private const string LabelRun = "<w:b/><w:color w:val=\"1F4E79\"/><w:sz w:val=\"20\"/><w:szCs w:val=\"20\"/>";
    private const string MetaRun = "<w:color w:val=\"777777\"/><w:sz w:val=\"18\"/><w:szCs w:val=\"18\"/>";
    private const string SubtitleRun = "<w:i/><w:color w:val=\"666666\"/>";
    private const string LabelSpacing = "<w:spacing w:before=\"120\" w:after=\"20\"/>";
    private const string MetaSpacing = "<w:spacing w:after=\"80\"/>";
    private const string SubtitleSpacing = "<w:spacing w:after=\"240\"/>";

    public static void Export(IReadOnlyList<Song> songs, string path, string title, bool pageBreakPerSong)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        AddText(zip, "[Content_Types].xml", ContentTypes);
        AddText(zip, "_rels/.rels", RootRels);
        AddText(zip, "word/_rels/document.xml.rels", DocRels);
        AddText(zip, "word/styles.xml", Styles);
        AddText(zip, "word/footer1.xml", Footer);

        ZipArchiveEntry entry = zip.CreateEntry("word/document.xml", CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        WriteDocument(w, songs, title, pageBreakPerSong);
    }

    private static void WriteDocument(TextWriter w, IReadOnlyList<Song> songs, string title, bool pageBreakPerSong)
    {
        w.Write(DocStart);

        Para(w, string.IsNullOrWhiteSpace(title) ? "Cancioneiro" : title.Trim(), style: "Title");
        Para(w,
            $"{songs.Count} músicas exportadas do EasyWorship 7. Cada música é um título (painel de navegação do Word) seguido dos slides com a respetiva etiqueta.",
            spacing: SubtitleSpacing, rPr: SubtitleRun);

        bool first = true;
        foreach (Song s in songs)
        {
            Para(w, s.Title, style: "Heading1", keepNext: true, pageBreakBefore: pageBreakPerSong && !first);
            first = false;

            string meta = string.Join("  ·  ", new[] { s.Author, s.Copyright }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (meta.Length > 0) Para(w, meta, keepNext: true, spacing: MetaSpacing, rPr: MetaRun);

            foreach (Slide sl in s.Slides)
            {
                if (sl.Label.Length > 0) Para(w, sl.Label, keepNext: true, spacing: LabelSpacing, rPr: LabelRun);

                string[] lines = sl.Text.Split('\n').Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToArray();
                for (int i = 0; i < lines.Length; i++)
                {
                    Para(w, lines[i], keepNext: i < lines.Length - 1, keepLines: true);
                }
            }
        }

        w.Write(DocEnd);
    }

    private static void Para(
        TextWriter w,
        string text,
        string? style = null,
        bool keepNext = false,
        bool keepLines = false,
        bool pageBreakBefore = false,
        string? spacing = null,
        string? rPr = null)
    {
        w.Write("<w:p>");
        if (style != null || keepNext || keepLines || pageBreakBefore || spacing != null)
        {
            w.Write("<w:pPr>");
            if (style != null) w.Write("<w:pStyle w:val=\"" + style + "\"/>");
            if (keepNext) w.Write("<w:keepNext/>");
            if (keepLines) w.Write("<w:keepLines/>");
            if (pageBreakBefore) w.Write("<w:pageBreakBefore/>");
            if (spacing != null) w.Write(spacing);
            w.Write("</w:pPr>");
        }
        w.Write("<w:r>");
        if (rPr != null) w.Write("<w:rPr>" + rPr + "</w:rPr>");
        w.Write("<w:t xml:space=\"preserve\">");
        w.Write(Escape(text));
        w.Write("</w:t></w:r></w:p>");
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length + 16);
        foreach (char c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '\t': sb.Append(c); break;
                default:
                    // Caracteres de controlo não são permitidos em XML 1.0.
                    if (c >= 0x20 && c != '￾' && c != '￿') sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static void AddText(ZipArchive zip, string name, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }
}
