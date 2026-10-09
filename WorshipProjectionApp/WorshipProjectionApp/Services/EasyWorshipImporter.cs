using System.IO;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.Sqlite;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services;

public static class EasyWorshipImporter
{
    public static List<Song> ImportFromEasyWorshipSqlite(string ewDbFilePath)
    {
        var songs = new List<Song>();

        if (!File.Exists(ewDbFilePath))
            throw new FileNotFoundException("Ficheiro Songs.db do EasyWorship não encontrado.");

        // Abre a base de dados original em modo Read-Only para evitar conflitos de lock
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = ewDbFilePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connStr);
        connection.Open();

        // No EasyWorship v6/v7 a tabela principal chama-se 'song'
        // As colunas de texto contêm tipicamente o título e a letra ('words')
        const string query = "SELECT title, author, words FROM song WHERE words IS NOT NULL AND words != ''";

        var rows = connection.Query(query);

        foreach (var row in rows)
        {
            string title = row.title ?? "Sem Título";
            string? author = row.author;
            string rawWords = row.words ?? string.Empty;

            string cleanLyrics = CleanEasyWorshipWords(rawWords);

            if (!string.IsNullOrWhiteSpace(cleanLyrics))
            {
                songs.Add(new Song
                {
                    Title = title.Trim(),
                    Artist = author?.Trim(),
                    RawLyrics = cleanLyrics
                });
            }
        }

        return songs;
    }

    private static string CleanEasyWorshipWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Se o texto vier empacotado em RTF simples do EasyWorship, limpa as tags básicas
        if (input.StartsWith("{\\rtf"))
        {
            // Remove comandos RTF como \par, \b, etc.
            string plain = Regex.Replace(input, @"\\[a-z]+[0-9]*\s?", " ");
            plain = Regex.Replace(plain, @"[{}]", "");
            return plain.Trim();
        }

        return input.Trim();
    }
}