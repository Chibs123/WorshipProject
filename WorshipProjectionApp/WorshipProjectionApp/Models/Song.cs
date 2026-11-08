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

        public List<SlideItem> ParseSlides(int maxLinesPerSlide = 0)
        {
            var slides = new List<SlideItem>();
            if (string.IsNullOrWhiteSpace(RawLyrics)) return slides;

            // 1. Tenta decifrar como o NOVO formato PowerPoint (JSON)
            // Só tenta JSON se parecer mesmo uma lista de slides ("[{"); evita exceções lentas em letras que começam por "[Verso 1]"
            if (System.Text.RegularExpressions.Regex.IsMatch(RawLyrics, @"^\s*\[\s*(\{|\])"))
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
                var lines = block.Trim().Replace("\r", "").Split('\n');

                // Divide blocos longos em slides equilibrados (ex.: 7 linhas com máx. 4 => 4 + 3)
                if (maxLinesPerSlide > 0 && lines.Length > maxLinesPerSlide)
                {
                    int parts = (int)Math.Ceiling(lines.Length / (double)maxLinesPerSlide);
                    int perPart = (int)Math.Ceiling(lines.Length / (double)parts);
                    for (int i = 0; i < lines.Length; i += perPart)
                    {
                        slides.Add(new SlideItem
                        {
                            Header = $"Slide {slideNum++}",
                            Content = string.Join("\n", lines.Skip(i).Take(perPart))
                        });
                    }
                }
                else
                {
                    slides.Add(new SlideItem
                    {
                        Header = $"Slide {slideNum++}",
                        Content = block.Trim()
                    });
                }
            }

            return slides;
        }
    }
}