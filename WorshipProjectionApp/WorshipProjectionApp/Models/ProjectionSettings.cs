using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace WorshipProjectionApp.Models;

public class ProjectionSettings
{    public string AppThemeColorHex { get; set; } = "#22D3EE";
    public string ThemeId { get; set; } = "grafite-ciano";

    // --- Texto da projeção (tamanhos em pixéis num ecrã de 1080p; escalam com a altura do ecrã) ---
    public double TextFontSize { get; set; } = 84;
    public bool Uppercase { get; set; } = false;
    public bool OutlineEnabled { get; set; } = false;
    public double OutlineThickness { get; set; } = 4;
    public bool BackBoxEnabled { get; set; } = false;
    public double BackBoxOpacity { get; set; } = 0.55;
    public double DimmerOpacity { get; set; } = 0.15;
    public bool BibleRefAtBottom { get; set; } = true;
    public int MaxLinesPerSlide { get; set; } = 0; // 0 = não dividir

    // --- Ecrã do grupo de louvor (-1 = desativado) ---
    public int StageScreenIndex { get; set; } = -1;
    public double StageFontSize { get; set; } = 96;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 48;
    public string FontColorHex { get; set; } = "#FFFFFF";
    public bool IsBold { get; set; } = true;
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Center;
    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;
    public double LineSpacing { get; set; } = 1.2;
    public int MarginPercent { get; set; } = 8;
    public double ShadowBlurRadius { get; set; } = 14;
    public double ShadowOpacity { get; set; } = 0.95;
    public int ScreenIndex { get; set; } = 1;
    private static readonly string SettingsPath = WorshipProjectionApp.AppPaths.File("projection_settings.json");

    public static ProjectionSettings Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ProjectionSettings>(json) ?? new ProjectionSettings();
            }
            catch { }
        }
        return new ProjectionSettings();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}