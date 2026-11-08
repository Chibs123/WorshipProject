using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using PPT = Microsoft.Office.Interop.PowerPoint;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp.Services
{
    public class PowerPointService
    {
        // Pasta base onde vamos guardar as imagens exportadas
        private readonly string _pptExportFolder;

        public PowerPointService()
        {
            _pptExportFolder = WorshipProjectionApp.AppPaths.Folder("ImportedPPTs");
            if (!Directory.Exists(_pptExportFolder))
                Directory.CreateDirectory(_pptExportFolder);
        }

        public ScheduleItem? ImportAsImages(string pptxFilePath)
        {
            if (!File.Exists(pptxFilePath)) return null;

            PPT.Application? pptApp = null;
            PPT.Presentation? presentation = null;

            string safeName = Path.GetFileNameWithoutExtension(pptxFilePath);
            string slideFolder = Path.Combine(_pptExportFolder, safeName + "_" + DateTime.Now.ToString("HHmmss"));
            Directory.CreateDirectory(slideFolder);

            // 1. Criar o PowerPoint como um item normal para a lista de programação
            var pptItem = new ScheduleItem
            {
                Title = safeName,
                Subtitle = "PowerPoint",
                ItemTypeIcon = "📊",
                IsFolder = false // Fica como uma música/item normal!
            };

            try
            {
                pptApp = new PPT.Application();

                // Usamos 'dynamic' para contornar a exigência do compilador pela DLL MsoTriState
                dynamic presentations = pptApp.Presentations;

                // Open(FileName, ReadOnly (-1 = msoTrue), Untitled (0 = msoFalse), WithWindow (0 = msoFalse))
                presentation = presentations.Open(pptxFilePath, -1, 0, 0);

                // 2. Extrair cada slide e adicionar à lista de Slides deste item
                dynamic slides = presentation.Slides;
                int slideCount = slides.Count;
                for (int i = 1; i <= slideCount; i++)
                {
                    string imagePath = Path.Combine(slideFolder, $"Slide_{i}.jpg");

                    // Exportar em Full HD (1920x1080)
                    dynamic slide = slides[i];
                    slide.Export(imagePath, "jpg", 1920, 1080);
                    Marshal.ReleaseComObject(slide);

                    // Criar o slide perfeitamente alinhado com a tua classe SlideItem
                    pptItem.Slides.Add(new SlideItem
                    {
                        Header = $"Slide {i}",
                        Content = "", // O texto fica vazio pois a imagem já tem tudo visível
                        BackgroundPath = imagePath // <-- É só isto! O HasCustomBackground atualiza sozinho!
                    });
                }
            }
            finally
            {
                // Não mostra MessageBox aqui (corre numa thread de fundo): a exceção sobe para o chamador.
                try { presentation?.Close(); } catch { }
                if (presentation != null) Marshal.ReleaseComObject(presentation);
                try { pptApp?.Quit(); } catch { }
                if (pptApp != null) Marshal.ReleaseComObject(pptApp);
            }

            return pptItem;
        }
    }
}