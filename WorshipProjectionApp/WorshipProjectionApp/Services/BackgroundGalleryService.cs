using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NReco.VideoConverter;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services
{
    public class BackgroundGalleryService
    {
        public string BackgroundsFolder { get; }
        public string ThumbnailsFolder { get; }

        public BackgroundGalleryService()
        {
            // Define as pastas base
            BackgroundsFolder = WorshipProjectionApp.AppPaths.Folder("Backgrounds");
            ThumbnailsFolder = Path.Combine(BackgroundsFolder, "Thumbnails");

            // Cria as pastas se não existirem
            if (!Directory.Exists(BackgroundsFolder))
            {
                Directory.CreateDirectory(BackgroundsFolder);
            }
            if (!Directory.Exists(ThumbnailsFolder))
            {
                Directory.CreateDirectory(ThumbnailsFolder);
            }
        }

        public List<MediaBackground> GetBackgrounds()
        {
            var list = new List<MediaBackground>();
            var videoExts = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

            // Pega apenas nos ficheiros da pasta principal (ignora a pasta Thumbnails)
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
                        ThumbnailPath = ThumbnailFor(file), // Caminho esperado; os que faltam são gerados em paralelo abaixo
                        IsVideo = true
                    });
                }
                else if (imageExts.Contains(ext))
                {
                    list.Add(new MediaBackground
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file,
                        ThumbnailPath = file, // Se for imagem, a miniatura é a própria imagem
                        IsVideo = false
                    });
                }
            }

            // Gera em paralelo as miniaturas que ainda não existem (só acontece na primeira vez)
            var missing = list.Where(b => b.IsVideo && !File.Exists(b.ThumbnailPath)).ToList();
            if (missing.Count > 0)
            {
                Parallel.ForEach(missing,
                    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) },
                    b => b.ThumbnailPath = GetOrGenerateThumbnail(b.FilePath));
            }

            return list;
        }

        private string ThumbnailFor(string videoFilePath) =>
            Path.Combine(ThumbnailsFolder, Path.GetFileNameWithoutExtension(videoFilePath) + ".jpg");

        // Copia um ficheiro do Windows para a pasta interna e gera a miniatura
        public MediaBackground? ImportFile(string sourceFilePath)
        {
            try
            {
                if (!File.Exists(sourceFilePath)) return null;

                string fileName = Path.GetFileName(sourceFilePath);
                string destination = Path.Combine(BackgroundsFolder, fileName);

                // Evita erro se tentarmos importar um ficheiro que já está na própria pasta
                if (!File.Exists(destination) && !sourceFilePath.Equals(destination, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFilePath, destination, true);
                }

                string ext = Path.GetExtension(destination).ToLower();
                var videoExts = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };
                bool isVideo = videoExts.Contains(ext);

                return new MediaBackground
                {
                    Name = Path.GetFileNameWithoutExtension(destination),
                    FilePath = destination,
                    ThumbnailPath = isVideo ? GetOrGenerateThumbnail(destination) : destination,
                    IsVideo = isVideo
                };
            }
            catch
            {
                return null;
            }
        }

        // MOTOR DE EXTRAÇÃO DE FRAMES
        private string GetOrGenerateThumbnail(string videoFilePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(videoFilePath);
                string thumbnailPath = Path.Combine(ThumbnailsFolder, fileName + ".jpg");

                // Só processa se o JPG ainda não existir na pasta (poupa muito tempo a carregar a app)
                if (!File.Exists(thumbnailPath))
                {
                    var ffMpeg = new FFMpegConverter();
                    // Extrai o frame ao segundo 1.0 (para não apanhar ecrãs pretos de fade-in inicial)
                    ffMpeg.GetVideoThumbnail(videoFilePath, thumbnailPath, 1.0f);
                }

                return thumbnailPath;
            }
            catch
            {
                // Se o vídeo estiver corrompido ou houver erro, devolve o caminho original para não crashar
                return videoFilePath;
            }
        }
    }
}