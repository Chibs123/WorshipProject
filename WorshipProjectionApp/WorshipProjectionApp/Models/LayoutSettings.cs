using System.IO;
using System.Text.Json;

namespace WorshipProjectionApp.Models;

public enum PanelType
{
    Resources, // Músicas & Bíblia
    Schedule,  // Rol do Culto
    Slides,    // Letras / Slides Ativos
    Live,       // Em Direto / Monitor
    WorshipTeam  // Grupo de Louvor & Áudio
}

public class PanelConfig
{
    public PanelType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Row { get; set; } = 0;        // Linha inicial: 0 a 3
    public int Column { get; set; } = 0;     // Coluna inicial: 0 a 3
    public int RowSpan { get; set; } = 2;    // Altura: 1 a 4
    public int ColumnSpan { get; set; } = 2; // Largura: 1 a 4
    public bool IsVisible { get; set; } = true;
}

public class LayoutSettings
{
    public List<PanelConfig> Panels { get; set; } = new();

    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "layout_settings.json");

    public static LayoutSettings LoadDefault()
    {
        return new LayoutSettings
        {
            Panels = new List<PanelConfig>
            {
                new() { Type = PanelType.Resources, Title = "🎵 Recursos",   Row = 0, Column = 0, RowSpan = 2, ColumnSpan = 2 },
                new() { Type = PanelType.Schedule,  Title = "📜 Rol Culto",  Row = 0, Column = 2, RowSpan = 2, ColumnSpan = 2 },
                new() { Type = PanelType.Slides,    Title = "📑 Slides",     Row = 2, Column = 0, RowSpan = 2, ColumnSpan = 2 },
                new() { Type = PanelType.Live,      Title = "🔴 Em Direto",  Row = 2, Column = 2, RowSpan = 2, ColumnSpan = 2 },
                new() { Type = PanelType.WorshipTeam, Title = "🎤 Equipa Louvor", Row = 2, Column = 0, RowSpan = 2, ColumnSpan = 2 }
            }
        };
    }

    public static LayoutSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<LayoutSettings>(json);
                if (loaded != null && loaded.Panels.Count > 0)
                {
                    // Garante que todos os tipos de painel do enum existem na lista
                    var allTypes = Enum.GetValues<PanelType>();
                    bool missingAny = false;

                    foreach (var type in allTypes)
                    {
                        if (!loaded.Panels.Any(p => p.Type == type))
                        {
                            // Se faltar o WorshipTeam (ou outro), vai buscar a definição predefinida
                            var defaultPanel = LoadDefault().Panels.FirstOrDefault(p => p.Type == type);
                            if (defaultPanel != null)
                            {
                                loaded.Panels.Add(defaultPanel);
                                missingAny = true;
                            }
                        }
                    }

                    if (missingAny)
                    {
                        loaded.Save();
                    }

                    return loaded;
                }
            }
        }
        catch { }

        var def = LoadDefault();
        def.Save();
        return def;
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}