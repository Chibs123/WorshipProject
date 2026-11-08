namespace EasyWorshipParaWord;

/// <summary>Um slide de uma música: etiqueta (Verse, Chorus, End...) e a letra em texto simples.</summary>
public sealed record Slide(string Label, string Text);

public sealed record Song(
    long Id,
    string Title,
    string Author,
    string Copyright,
    string Tags,
    string Reference,
    List<Slide> Slides);

/// <summary>Resultado da leitura das duas bases de dados.</summary>
public sealed record ReadResult(List<Song> Songs, List<string> Warnings);
