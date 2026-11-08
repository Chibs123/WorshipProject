using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyWorshipParaWord;

/// <summary>
/// Converte o RTF que o EasyWorship 7 guarda em SongWords.db (coluna "words") em slides de texto simples.
/// Há dois formatos: o mais recente (um grupo {\pard ...} por parágrafo, com marcadores de slide e
/// etiquetas ocultas) e o antigo (um único bloco, slides separados por linha em branco).
/// </summary>
public static class RtfParser
{
    private sealed class Group
    {
        public bool Ignore;
        public bool Dest;
        public bool Pard;
        public bool Hidden;
        public bool Marker;
        public StringBuilder Text = new();
    }

    private sealed class Para
    {
        public string Text = "";
        public bool Hidden;
        public bool Marker;
    }

    private sealed class SlideBuilder
    {
        public string Label = "";
        public List<string> Lines = new();
    }

    private static readonly Regex ControlWord = new(@"\G([a-zA-Z]+)(-?\d+)? ?", RegexOptions.Compiled);
    private static readonly Regex BlankLineSplit = new(@"\n[ \t]*\n", RegexOptions.Compiled);

    private static Encoding Win1252
    {
        get
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }
    }

    public static List<Slide> ToSlides(string rtf)
    {
        var paras = ParseParagraphs(rtf);
        return SlidesFrom(paras);
    }

    private static List<Para> ParseParagraphs(string rtf)
    {
        var output = new List<Para>();
        var stack = new List<Group> { new Group { Ignore = true } };
        var cp1252 = Win1252;
        int i = 0, n = rtf.Length, ucSkip = 0;

        while (i < n)
        {
            char c = rtf[i];
            Group top = stack[^1];

            if (c == '{')
            {
                var g = new Group { Ignore = top.Ignore && stack.Count > 1 && top.Dest };
                stack.Add(g);
                if (stack.Count == 2) g.Ignore = false;
                i++;
            }
            else if (c == '}')
            {
                if (stack.Count > 1)
                {
                    Group g = stack[^1];
                    stack.RemoveAt(stack.Count - 1);
                    if (g.Pard)
                    {
                        output.Add(new Para { Text = g.Text.ToString(), Hidden = g.Hidden, Marker = g.Marker });
                    }
                    else if (!g.Ignore)
                    {
                        stack[^1].Text.Append(g.Text);
                    }
                }
                i++;
            }
            else if (c == '\\')
            {
                i++;
                if (i >= n) break;
                char d = rtf[i];

                if (d == '\'')
                {
                    string hx = i + 3 <= n ? rtf.Substring(i + 1, 2) : "";
                    i += 3;
                    if (ucSkip > 0) { ucSkip--; continue; }
                    if (int.TryParse(hx, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
                    {
                        top.Text.Append(cp1252.GetString(new[] { (byte)b }));
                    }
                }
                else if (d == '*')
                {
                    top.Ignore = true;
                    top.Dest = true;
                    i++;
                }
                else if (d == '\\' || d == '{' || d == '}')
                {
                    top.Text.Append(d);
                    i++;
                }
                else if (d == '~')
                {
                    top.Text.Append(' ');
                    i++;
                }
                else if (d == '-' || d == '_' || d == '|' || d == ':')
                {
                    i++;
                }
                else if (char.IsAsciiLetter(d))
                {
                    Match m = ControlWord.Match(rtf, i);
                    string word = m.Groups[1].Value;
                    string arg = m.Groups[2].Success ? m.Groups[2].Value : "";
                    i = m.Index + m.Length;

                    switch (word)
                    {
                        case "fonttbl":
                        case "colortbl":
                        case "stylesheet":
                        case "info":
                        case "pict":
                            top.Ignore = true;
                            top.Dest = true;
                            break;
                        case "pard":
                            top.Pard = true;
                            break;
                        case "sdslidemarker":
                            top.Marker = true;
                            break;
                        case "sdparawysiwghidden":
                            top.Hidden = true;
                            break;
                        case "par":
                        case "line":
                            top.Text.Append('\n');
                            break;
                        case "tab":
                            top.Text.Append('\t');
                            break;
                        case "u":
                            if (arg.Length > 0 && int.TryParse(arg, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int v))
                            {
                                if (v < 0) v += 65536;
                                if (!top.Ignore) top.Text.Append((char)v);
                                ucSkip = 1;
                            }
                            break;
                        case "emdash":
                            top.Text.Append('—');
                            break;
                        case "endash":
                            top.Text.Append('–');
                            break;
                        case "lquote":
                        case "rquote":
                            top.Text.Append('\'');
                            break;
                        case "ldblquote":
                        case "rdblquote":
                            top.Text.Append('"');
                            break;
                    }
                }
                else
                {
                    i++;
                }
            }
            else if (c == '\r' || c == '\n')
            {
                i++;
            }
            else
            {
                if (ucSkip > 0)
                {
                    ucSkip--;
                    i++;
                    continue;
                }
                if (!top.Ignore) top.Text.Append(c);
                i++;
            }
        }

        return output;
    }

    private static List<Slide> SlidesFrom(List<Para> paras)
    {
        bool newDialect = paras.Any(p => p.Hidden || p.Marker);
        var result = new List<Slide>();

        if (newDialect)
        {
            var slides = new List<SlideBuilder>();
            SlideBuilder? cur = null;
            foreach (var p in paras)
            {
                string t = p.Text.Trim();
                if (p.Hidden)
                {
                    cur = new SlideBuilder { Label = t };
                    slides.Add(cur);
                    continue;
                }
                if (cur == null)
                {
                    cur = new SlideBuilder();
                    slides.Add(cur);
                }
                if (t.Length > 0) cur.Lines.Add(t);
                if (p.Marker) cur = null;
            }
            foreach (var s in slides)
            {
                string text = string.Join("\n", s.Lines).Trim('\n');
                if (s.Label.Length > 0 || text.Length > 0) result.Add(new Slide(s.Label, text));
            }
            return result;
        }

        // Formato antigo: um único bloco; slides separados por linha em branco; 1.ª linha = etiqueta.
        string raw = string.Join("\n", paras.Select(p => p.Text)).Replace("\r", "");
        foreach (string block in BlankLineSplit.Split(raw))
        {
            string[] lines = block.Trim('\n').Split('\n').Select(l => l.TrimEnd()).ToArray();
            if (!lines.Any(l => l.Trim().Length > 0)) continue;
            string label = lines[0].Trim();
            string text = string.Join("\n", lines.Skip(1)).Trim('\n');
            result.Add(new Slide(label, text));
        }
        return result;
    }
}
