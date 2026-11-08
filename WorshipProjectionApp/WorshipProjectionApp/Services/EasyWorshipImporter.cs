using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services;

/// <summary>
/// Importa músicas do EasyWorship 7. Os títulos estão em Songs.db (tabela 'song')
/// e as letras (RTF) em SongWords.db (tabela 'word', ligada por song_id = song.rowid).
/// </summary>
public static class EasyWorshipImporter
{
    private static readonly Regex LabelLine = new(
        @"^(verse|chorus|bridge|pre-?chorus|intro|tag|ending|outro|refr[aã]o|verso|ponte|coro)\s*\d*\s*(\(\d+x\))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<Song> ImportFromEasyWorshipSqlite(string songsDbPath)
    {
        if (!File.Exists(songsDbPath))
            throw new FileNotFoundException("Ficheiro Songs.db do EasyWorship não encontrado.");

        string wordsDbPath = Path.Combine(Path.GetDirectoryName(songsDbPath) ?? "", "SongWords.db");
        if (!File.Exists(wordsDbPath))
            throw new FileNotFoundException(
                "O ficheiro SongWords.db tem de estar na mesma pasta do Songs.db (é lá que o EasyWorship guarda as letras).");

        // Lê as letras (SongWords.db) para memória, indexadas por song_id
        var lyricsById = new Dictionary<long, string>();
        using (var wordsConn = new SqliteConnection(ReadOnly(wordsDbPath)))
        {
            wordsConn.Open();
            foreach (var row in wordsConn.Query("SELECT song_id, words FROM word"))
            {
                if (row.song_id == null || row.words == null) continue;
                lyricsById[(long)row.song_id] = (string)row.words;
            }
        }

        var songs = new List<Song>();
        using var songsConn = new SqliteConnection(ReadOnly(songsDbPath));
        songsConn.Open();

        foreach (var row in songsConn.Query("SELECT rowid AS id, title, author FROM song"))
        {
            long id = (long)row.id;
            if (!lyricsById.TryGetValue(id, out var rtf)) continue;

            string lyrics = RtfToSlides(rtf);
            if (string.IsNullOrWhiteSpace(lyrics)) continue;

            string title = ((string?)row.title)?.Trim() ?? "";
            songs.Add(new Song
            {
                Title = string.IsNullOrEmpty(title) ? "Sem Título" : title,
                Artist = ((string?)row.author)?.Trim() ?? "",
                RawLyrics = lyrics
            });
        }

        return songs;
    }

    private static string ReadOnly(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();

    // ---------------------------------------------------------------
    // RTF do EasyWorship -> texto com slides separados por linha em branco
    // ---------------------------------------------------------------
    public static string RtfToSlides(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        if (!input.TrimStart().StartsWith("{\\rtf")) return input.Trim();

        var slides = new List<string>();
        var current = new List<string>();

        void Flush()
        {
            if (current.Count > 0) { slides.Add(string.Join("\n", current)); current.Clear(); }
        }

        if (input.Contains("\\sdslidemarker"))
        {
            // Formato novo (EW7): cada parágrafo é um grupo \pard; há marcadores de slide e etiquetas ocultas
            var chunks = Regex.Split(input, @"\\pard\b").Skip(1);
            foreach (var chunk in chunks)
            {
                if (chunk.Contains("\\sdslidemarker")) { Flush(); continue; }
                if (chunk.Contains("\\sdparawysiwghidden")) continue; // etiqueta tipo "Verse"
                string text = DecodeRtf(chunk).Trim();
                if (text.Length > 0) current.Add(text);
            }
            Flush();
        }
        else
        {
            // Formato antigo: um só bloco; linha em branco = novo slide; linhas "Verse/Chorus" = etiquetas
            int k = input.IndexOf("\\pard", StringComparison.Ordinal);
            string body = DecodeRtf(k >= 0 ? input.Substring(k) : input);
            foreach (var raw in body.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || LabelLine.IsMatch(line)) { Flush(); continue; }
                current.Add(line);
            }
            Flush();
        }

        return string.Join("\n\n", slides);
    }

    private static string RemoveDestinations(string s)
    {
        // Remove grupos {\*\...} (com chaves aninhadas)
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            if (i + 2 < s.Length && s[i] == '{' && s[i + 1] == '\\' && s[i + 2] == '*')
            {
                int depth = 0;
                while (i < s.Length)
                {
                    if (s[i] == '{') depth++;
                    else if (s[i] == '}') { depth--; if (depth == 0) { i++; break; } }
                    i++;
                }
            }
            else sb.Append(s[i++]);
        }
        return sb.ToString();
    }

    private static string DecodeRtf(string s)
    {
        s = RemoveDestinations(s);
        // \uN? -> caractere unicode (o '?' é só o substituto)
        s = Regex.Replace(s, @"\\u(-?\d+)\??", m => ((char)(short)int.Parse(m.Groups[1].Value)).ToString());
        // \'hh -> byte cp1252
        s = Regex.Replace(s, @"\\'([0-9a-fA-F]{2})", m =>
        {
            try { return Encoding.GetEncoding(1252).GetString(new[] { Convert.ToByte(m.Groups[1].Value, 16) }); }
            catch { return Encoding.Latin1.GetString(new[] { Convert.ToByte(m.Groups[1].Value, 16) }); }
        });
        s = Regex.Replace(s, @"\\(line|par)\b ?", "\n");
        s = Regex.Replace(s, @"\\[a-zA-Z]+-?\d* ?", "");
        s = s.Replace("\\\\", "\\").Replace("\\{", "{").Replace("\\}", "}");
        return s.Replace("{", "").Replace("}", "").Replace("\r", "");
    }
}
