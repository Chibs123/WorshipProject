using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WorshipProjectionApp.Models
{
    public class Song
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;

        // Agora esta coluna na DB vai guardar o formato JSON dos slides
        public string RawLyrics { get; set; } = string.Empty;

        public List<SlideItem> ParseSlides()
        {
            var slides = new List<SlideItem>();
            if (string.IsNullOrWhiteSpace(RawLyrics)) return slides;

            // 1. Tenta decifrar como o NOVO formato PowerPoint (JSON)
            if (RawLyrics.TrimStart().StartsWith("["))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<SlideItem>>(RawLyrics);
                    if (parsed != null) return parsed;
                }
                catch { /* Falhou, avança para a leitura em texto antigo */ }
            }

            // 2. RETROCOMPATIBILIDADE: Se for uma música antiga (texto corrido), 
            // divide por parágrafos duplos e converte em slides automaticamente.
            var blocks = RawLyrics.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            int slideNum = 1;

            foreach (var block in blocks)
            {
                slides.Add(new SlideItem
                {
                    Header = $"Slide {slideNum++}",
                    Content = block.Trim()
                });
            }

            return slides;
        }
    }
}