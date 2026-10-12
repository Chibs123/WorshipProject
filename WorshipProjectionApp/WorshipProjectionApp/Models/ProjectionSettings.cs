using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace WorshipProjectionApp.Models;

public class ProjectionSettings
{
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
    public int ScreenIndex { get; internal set; }

    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "projection_settings.json");

    public static ProjectionSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<ProjectionSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch { }

        return new ProjectionSettings();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}