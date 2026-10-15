using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services;

public class DatabaseService
{
    private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "worship.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    public DatabaseService()
    {
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        const string sql = @"
            CREATE TABLE IF NOT EXISTS Songs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Artist TEXT,
                RawLyrics TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Songs_Title ON Songs(Title);
        ";
        connection.Execute(sql);
    }

    public List<Song> GetAllSongs()
    {
        using var connection = new SqliteConnection(ConnectionString);
        return connection.Query<Song>("SELECT Id, Title, Artist, RawLyrics FROM Songs ORDER BY Title ASC").AsList();
    }

    public void InsertSong(string title, string? artist, string lyrics)
    {
        using var connection = new SqliteConnection(ConnectionString);
        const string sql = "INSERT INTO Songs (Title, Artist, RawLyrics) VALUES (@Title, @Artist, @RawLyrics)";
        connection.Execute(sql, new { Title = title, Artist = artist, RawLyrics = lyrics });
    }

    public void UpdateSong(Song song)
    {
        // Assume que a tua string de conexão se chama _connectionString (ou semelhante)
        // Se a tua variável tiver outro nome (ex: dbPath), ajusta a linha abaixo.
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        string sql = @"
        UPDATE Songs 
        SET Title = @Title, Artist = @Artist, RawLyrics = @RawLyrics 
        WHERE Id = @Id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Title", song.Title ?? "");
        command.Parameters.AddWithValue("@Artist", song.Artist ?? "");
        command.Parameters.AddWithValue("@RawLyrics", song.RawLyrics ?? "");
        command.Parameters.AddWithValue("@Id", song.Id);

        command.ExecuteNonQuery();
    }

    public void DeleteSong(int id)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        const string sql = "DELETE FROM Songs WHERE Id = @Id";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        command.ExecuteNonQuery();
    }

    public void InsertBatch(IEnumerable<Song> songs)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        const string sql = "INSERT INTO Songs (Title, Artist, RawLyrics) VALUES (@Title, @Artist, @RawLyrics)";
        connection.Execute(sql, songs, transaction);
        transaction.Commit();
    }
}