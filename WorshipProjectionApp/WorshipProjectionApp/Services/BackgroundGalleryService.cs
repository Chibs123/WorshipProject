using System.IO;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services;

public class BackgroundGalleryService
{
    public string BackgroundsFolder { get; }

    public BackgroundGalleryService()
    {
        BackgroundsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backgrounds");
        if (!Directory.Exists(BackgroundsFolder))
        {
            Directory.CreateDirectory(BackgroundsFolder);
        }
    }

    public List<MediaBackground> GetBackgrounds()
    {
        var list = new List<MediaBackground>();
        var videoExts = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };
        var imageExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

        var files = Directory.GetFiles(BackgroundsFolder);
        foreach (var file in files)
        {
            string ext = Path.GetExtension(file).ToLower();
            if (videoExts.Contains(ext))
            {
                list.Add(new MediaBackground
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    IsVideo = true
                });
            }
            else if (imageExts.Contains(ext))
            {
                list.Add(new MediaBackground
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    IsVideo = false
                });
            }
        }

        return list;
    }

    // Copia um ficheiro do Windows para a pasta interna
    public MediaBackground? ImportFile(string sourceFilePath)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return null;

            string fileName = Path.GetFileName(sourceFilePath);
            string destination = Path.Combine(BackgroundsFolder, fileName);

            // Se ainda não existir na pasta, copia
            if (!File.Exists(destination))
            {
                File.Copy(sourceFilePath, destination, true);
            }

            string ext = Path.GetExtension(destination).ToLower();
            var videoExts = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };

            return new MediaBackground
            {
                Name = Path.GetFileNameWithoutExtension(destination),
                FilePath = destination,
                IsVideo = videoExts.Contains(ext)
            };
        }
        catch
        {
            return null;
        }
    }
}