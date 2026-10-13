using System;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace WorshipProjectionApp.Models
{
    public class MediaBackground
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public string ThumbnailPath { get; set; }
        public bool IsVideo { get; set; }

        // A cache interna da imagem para não ser lida do disco repetidamente
        private BitmapImage? _optimizedThumb;

        // [JsonIgnore] impede que a app tente guardar esta imagem pesada no ficheiro de texto
        [JsonIgnore]
        public BitmapImage? OptimizedThumbnail
        {
            get
            {
                if (_optimizedThumb != null) return _optimizedThumb;
                if (string.IsNullOrEmpty(ThumbnailPath) || !System.IO.File.Exists(ThumbnailPath)) return null;

                try
                {
                    _optimizedThumb = new BitmapImage();
                    _optimizedThumb.BeginInit();
                    _optimizedThumb.CacheOption = BitmapCacheOption.OnLoad;
                    _optimizedThumb.UriSource = new Uri(ThumbnailPath, UriKind.Absolute);

                    // O SEGREDO DA MEMÓRIA: Reduz a leitura para 150px de largura
                    _optimizedThumb.DecodePixelWidth = 150;

                    _optimizedThumb.EndInit();
                    _optimizedThumb.Freeze();
                }
                catch
                {
                    return null;
                }

                return _optimizedThumb;
            }
        }
    }
}