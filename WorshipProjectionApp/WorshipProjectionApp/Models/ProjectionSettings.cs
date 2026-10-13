using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace WorshipProjectionApp.Models;

public class ProjectionSettings
{    public string AppThemeColorHex { get; set; } = "#D4AF37";
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
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "projection_settings.json");

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