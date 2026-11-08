using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace EasyWorshipParaWord;

/// <summary>Lê Songs.db e SongWords.db (SQLite) do EasyWorship 7.</summary>
public static class SongReader
{
    public static ReadResult Read(string songsDbPath, string wordsDbPath)
    {
        // Trabalha sobre uma cópia temporária: não bloqueia nem altera os originais
        // (e funciona mesmo que o EasyWorship esteja aberto).
        string tmp = Path.Combine(Path.GetTempPath(), "ew2word_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            string songsCopy = CopyWithSidecars(songsDbPath, Path.Combine(tmp, "Songs.db"));
            string wordsCopy = CopyWithSidecars(wordsDbPath, Path.Combine(tmp, "SongWords.db"));

            var words = new Dictionary<long, (string Rtf, int SlideUids)>();
            using (var conn = Open(wordsCopy))
            {
                RequireTable(conn, "word", "SongWords.db");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT song_id, words, slide_uids FROM word";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (r.IsDBNull(0)) continue;
                    long songId = r.GetInt64(0);
                    string rtf = ReadText(r, 1);
                    int uids = r.IsDBNull(2)
                        ? 0
                        : r.GetString(2).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
                    words[songId] = (rtf, uids);
                }
            }

            var songs = new List<Song>();
            var warnings = new List<string>();
            using (var conn = Open(songsCopy))
            {
                RequireTable(conn, "song", "Songs.db");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT rowid, title, author, copyright, tags, reference_number FROM song";
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    long id = r.GetInt64(0);
                    string title = Str(r, 1);
                    var slides = new List<Slide>();
                    if (words.TryGetValue(id, out var w) && w.Rtf.Length > 0)
                    {
                        slides = RtfParser.ToSlides(w.Rtf);
                        if (w.SlideUids > 0 && w.SlideUids != slides.Count)
                        {
                            warnings.Add($"{title}: {slides.Count} slides lidos, {w.SlideUids} registados no EasyWorship — convém rever.");
                        }
                    }
                    else
                    {
                        warnings.Add($"{title}: sem letra em SongWords.db.");
                    }
                    songs.Add(new Song(id, title, Str(r, 2), Str(r, 3), Str(r, 4), Str(r, 5), slides));
                }
            }

            var pt = StringComparer.Create(new CultureInfo("pt-PT"), ignoreCase: true);
            songs.Sort((a, b) => pt.Compare(a.Title, b.Title));
            return new ReadResult(songs, warnings);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(tmp, recursive: true); } catch { /* ficheiros temporários: não é crítico */ }
        }
    }

    private static string CopyWithSidecars(string source, string dest)
    {
        File.Copy(source, dest, overwrite: true);
        foreach (string ext in new[] { "-wal", "-shm" })
        {
            if (File.Exists(source + ext)) File.Copy(source + ext, dest + ext, overwrite: true);
        }
        return dest;
    }

    private static SqliteConnection Open(string path)
    {
        var conn = new SqliteConnection($"Data Source={path};Pooling=False");
        conn.Open();
        // As colunas de texto do EasyWorship usam esta collation própria; registamos um equivalente.
        conn.CreateCollation("UTF8_U_CI", (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase));
        return conn;
    }

    private static void RequireTable(SqliteConnection conn, string table, string fileName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$n";
        cmd.Parameters.AddWithValue("$n", table);
        long count = (long)(cmd.ExecuteScalar() ?? 0L);
        if (count == 0)
        {
            throw new InvalidDataException(
                $"O ficheiro escolhido para {fileName} não tem a tabela '{table}'. Verifica se não trocaste os dois ficheiros.");
        }
    }

    private static string Str(SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "";

    private static string ReadText(SqliteDataReader r, int i)
    {
        if (r.IsDBNull(i)) return "";
        object v = r.GetValue(i);
        return v switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
        };
    }
}
