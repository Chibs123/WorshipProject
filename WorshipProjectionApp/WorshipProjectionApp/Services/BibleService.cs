using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services
{
    public class BibleService
    {
        public string BiblesFolder { get; }

        private static readonly Dictionary<string, string> BookAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "gn", "Gênesis" }, { "gen", "Gênesis" }, { "genesis", "Gênesis" },
            { "ex", "Êxodo" }, { "exodo", "Êxodo" },
            { "sl", "Salmos" }, { "sal", "Salmos" }, { "salmo", "Salmos" }, { "salmos", "Salmos" },
            { "pv", "Provérbios" }, { "pr", "Provérbios" }, { "proverbios", "Provérbios" },
            { "is", "Isaías" }, { "isa", "Isaías" }, { "isaias", "Isaías" },
            { "mt", "Mateus" }, { "mat", "Mateus" }, { "mateus", "Mateus" },
            { "mc", "Marcos" }, { "mar", "Marcos" }, { "marcos", "Marcos" },
            { "lc", "Lucas" }, { "luc", "Lucas" }, { "lucas", "Lucas" },
            { "jo", "João" }, { "joao", "João" },
            { "at", "Atos" }, { "atos", "Atos" },
            { "rm", "Romanos" }, { "rom", "Romanos" }, { "romanos", "Romanos" },
            { "1co", "1 Coríntios" }, { "2co", "2 Coríntios" },
            { "gl", "Gálatas" }, { "galatas", "Gálatas" },
            { "ef", "Efésios" }, { "efesios", "Efésios" },
            { "fp", "Filipenses" }, { "filipenses", "Filipenses" },
            { "cl", "Colossenses" }, { "colossenses", "Colossenses" },
            { "hb", "Hebreus" }, { "hebreus", "Hebreus" },
            { "tg", "Tiago" }, { "tiago", "Tiago" },
            { "1pe", "1 Pedro" }, { "2pe", "2 Pedro" },
            { "1jo", "1 João" }, { "2jo", "2 João" }, { "3jo", "3 João" },
            { "ap", "Apocalipse" }, { "apocalipse", "Apocalipse" }
        };

        public BibleService()
        {
            BiblesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Bibles");
            if (!Directory.Exists(BiblesFolder))
            {
                Directory.CreateDirectory(BiblesFolder);
            }
        }

        public List<string> GetAvailableVersions()
        {
            if (!Directory.Exists(BiblesFolder)) return new List<string>();

            var files = Directory.GetFiles(BiblesFolder, "*.sqlite")
                .Concat(Directory.GetFiles(BiblesFolder, "*.db"));

            return files.Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList();
        }

        public bool ImportBible(string sourceFilePath)
        {
            try
            {
                string fileName = Path.GetFileName(sourceFilePath);
                string destPath = Path.Combine(BiblesFolder, fileName);
                if (!File.Exists(destPath))
                {
                    File.Copy(sourceFilePath, destPath, true);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<BibleVerse> Search(string query, string version, bool searchByWords = false)
        {
            var finalResults = new List<BibleVerse>();
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(version)) return finalResults;

            query = query.Trim();
            string? dbPath = null;
            string[] extensions = { ".sqlite", ".db" };

            foreach (var ext in extensions)
            {
                string testPath = Path.Combine(BiblesFolder, version + ext);
                if (File.Exists(testPath)) { dbPath = testPath; break; }
            }

            if (dbPath == null)
            {
                finalResults.Add(new BibleVerse { BookName = "Aviso", Text = "Ficheiro não encontrado.", Version = version });
                return finalResults;
            }

            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                connection.Open();

                if (searchByWords)
                {
                    // MODO 2: PROCURA POR PALAVRAS NOS VERSÍCULOS
                    string sql = "SELECT b.name, v.chapter, v.verse, v.text FROM verse v JOIN book b ON v.book_id = b.id WHERE v.text LIKE @search LIMIT 40";
                    using var command = new SqliteCommand(sql, connection);
                    command.Parameters.AddWithValue("@search", "%" + query + "%");
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) finalResults.Add(new BibleVerse { BookName = reader.GetString(0), Chapter = reader.GetInt32(1), Verse = reader.GetInt32(2), Text = reader.GetString(3), Version = version });
                }
                else
                {
                    // MODO 1: REFERÊNCIA EXATA OU NOME DO LIVRO (ex: "Salm" ou "Jo 3:16")
                    // Regex atualizada: O Capítulo agora é opcional!
                    var match = Regex.Match(query, @"^(?<book>\d?\s*[a-zA-ZáéíóúÁÉÍÓÚãõÃÕâêîôûÂÊÎÔÛçÇ]+)(?:\s+(?<chapter>\d+))?(?:[:\s]?(?<verse>\d+))?(?:\s*-\s*(?<verseEnd>\d+))?$");

                    if (match.Success)
                    {
                        string bookInput = match.Groups["book"].Value.Replace(" ", "").ToLower();
                        string targetBookName = BookAliases.TryGetValue(bookInput, out var alias) ? alias : bookInput;

                        string sql = "SELECT b.name, v.chapter, v.verse, v.text FROM verse v JOIN book b ON v.book_id = b.id WHERE b.name LIKE @book";
                        using var command = new SqliteCommand();
                        command.Connection = connection;
                        command.Parameters.AddWithValue("@book", targetBookName + "%");

                        if (match.Groups["chapter"].Success)
                        {
                            sql += " AND v.chapter = @chapter";
                            command.Parameters.AddWithValue("@chapter", int.Parse(match.Groups["chapter"].Value));

                            if (match.Groups["verse"].Success && match.Groups["verseEnd"].Success)
                            {
                                sql += " AND v.verse >= @verse AND v.verse <= @verseEnd";
                                command.Parameters.AddWithValue("@verse", int.Parse(match.Groups["verse"].Value));
                                command.Parameters.AddWithValue("@verseEnd", int.Parse(match.Groups["verseEnd"].Value));
                            }
                            else if (match.Groups["verse"].Success)
                            {
                                sql += " AND v.verse = @verse";
                                command.Parameters.AddWithValue("@verse", int.Parse(match.Groups["verse"].Value));
                            }
                        }

                        sql += " ORDER BY v.chapter, v.verse LIMIT 40";
                        command.CommandText = sql;

                        using var reader = command.ExecuteReader();
                        while (reader.Read()) finalResults.Add(new BibleVerse { BookName = reader.GetString(0), Chapter = reader.GetInt32(1), Verse = reader.GetInt32(2), Text = reader.GetString(3), Version = version });
                    }
                }
            }
            catch (Exception ex)
            {
                finalResults.Add(new BibleVerse { BookName = "Erro", Text = ex.Message, Version = version });
            }

            return finalResults;
        }
    }
}