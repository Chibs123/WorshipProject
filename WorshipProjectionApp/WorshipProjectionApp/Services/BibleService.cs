using System.Text.RegularExpressions;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services;

public class BibleService
{
    private readonly List<BibleVerse> _verses = new();

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
        SeedSampleVerses();
    }

    private void SeedSampleVerses()
    {
        _verses.AddRange(new[]
        {
            new BibleVerse { BookNumber = 43, BookName = "João", Chapter = 3, Verse = 16, Text = "Porque Deus amou o mundo de tal maneira que deu o seu Filho unigênito, para que todo aquele que nele crê não pereça, mas tenha a vida eterna." },
            new BibleVerse { BookNumber = 43, BookName = "João", Chapter = 3, Verse = 17, Text = "Porque Deus enviou o seu Filho ao mundo, não para que condenasse o mundo, mas para que o mundo fosse salvo por ele." },
            new BibleVerse { BookNumber = 43, BookName = "João", Chapter = 14, Verse = 6, Text = "Disse-lhe Jesus: Eu sou o caminho, e a verdade e a vida; ninguém vem ao Pai, senão por mim." },
            new BibleVerse { BookNumber = 19, BookName = "Salmos", Chapter = 23, Verse = 1, Text = "O Senhor é o meu pastor, nada me faltará." },
            new BibleVerse { BookNumber = 19, BookName = "Salmos", Chapter = 23, Verse = 2, Text = "Deitar-me faz em verdes pastos, guia-me mansamente a águas tranquilas." },
            new BibleVerse { BookNumber = 19, BookName = "Salmos", Chapter = 91, Verse = 1, Text = "Aquele que habita no esconderijo do Altíssimo, à sombra do Onipotente descansará." },
            new BibleVerse { BookNumber = 45, BookName = "Romanos", Chapter = 8, Verse = 28, Text = "E sabemos que todas as coisas cooperam para o bem daqueles que amam a Deus, daqueles que são chamados segundo o seu propósito." },
            new BibleVerse { BookNumber = 50, BookName = "Filipenses", Chapter = 4, Verse = 13, Text = "Posso todas as coisas naquele que me fortalece." },
            new BibleVerse { BookNumber = 1, BookName = "Gênesis", Chapter = 1, Verse = 1, Text = "No princípio criou Deus os céus e a terra." }
        });
    }

    public List<BibleVerse> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<BibleVerse>();

        query = query.Trim();

        // Tenta fazer parse de padrão tipo "jo 3 16", "João 3:16", "1jo 1 9"
        var match = Regex.Match(query, @"^(?<book>\d?\s*[a-zA-ZáéíóúÁÉÍÓÚãõÃÕâêîôûÂÊÎÔÛçÇ]+)\s+(?<chapter>\d+)[:\s]?(?<verse>\d+)?$");
        if (match.Success)
        {
            string bookInput = match.Groups["book"].Value.Replace(" ", "").ToLower();
            string chapterStr = match.Groups["chapter"].Value;
            string verseStr = match.Groups["verse"].Value;

            int chapter = int.Parse(chapterStr);
            int? verse = string.IsNullOrEmpty(verseStr) ? null : int.Parse(verseStr);

            string targetBookName = BookAliases.TryGetValue(bookInput, out var alias) ? alias : bookInput;

            var queryResult = _verses.Where(v =>
                v.BookName.Equals(targetBookName, StringComparison.OrdinalIgnoreCase) ||
                v.BookName.StartsWith(targetBookName, StringComparison.OrdinalIgnoreCase));

            queryResult = queryResult.Where(v => v.Chapter == chapter);

            if (verse.HasValue)
            {
                queryResult = queryResult.Where(v => v.Verse == verse.Value);
            }

            return queryResult.OrderBy(v => v.Verse).ToList();
        }

        // Caso não seja referência, pesquisa por palavra-chave no texto
        return _verses
            .Where(v => v.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(30)
            .ToList();
    }
}