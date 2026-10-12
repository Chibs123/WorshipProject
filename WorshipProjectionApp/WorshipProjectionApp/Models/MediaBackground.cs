using System.IO;

namespace WorshipProjectionApp.Models;

public class MediaBackground
{
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsVideo { get; set; }

    // Usado pela interface para exibir miniatura (se for imagem mostra a própria imagem)
    public string ThumbnailPath => IsVideo ? string.Empty : FilePath;
}