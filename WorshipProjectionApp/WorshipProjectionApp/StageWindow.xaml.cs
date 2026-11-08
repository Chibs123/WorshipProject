using System.Windows;
using System.Windows.Media;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp;

/// <summary>Janela para o ecrã do grupo de louvor: mostra a letra atual (sem fundos nem avisos).</summary>
public partial class StageWindow : Window
{
    private ProjectionSettings _settings = new();
    private string _rawText = string.Empty;

    public StageWindow()
    {
        InitializeComponent();
        SizeChanged += (s, e) => ApplySettings(_settings);
    }

    public void SetText(string text)
    {
        _rawText = text ?? string.Empty;
        TxtStage.Text = _settings.Uppercase ? _rawText.ToUpperInvariant() : _rawText;
    }

    public void ApplySettings(ProjectionSettings settings)
    {
        _settings = settings;

        double w = ActualWidth > 0 ? ActualWidth : 1920;
        double h = ActualHeight > 0 ? ActualHeight : 1080;
        double scale = h / 1080.0;
        double fontSize = Math.Max(10, settings.StageFontSize * scale);

        TxtStage.FontFamily = new System.Windows.Media.FontFamily(settings.FontFamily);
        TxtStage.FontWeight = settings.IsBold ? FontWeights.Bold : FontWeights.Normal;
        TxtStage.TextAlignment = settings.TextAlignment;
        TxtStage.FontSize = fontSize;
        TxtStage.LineHeight = fontSize * Math.Max(0.8, settings.LineSpacing);

        System.Windows.Media.Brush? brush = null;
        try { brush = new BrushConverter().ConvertFromString(settings.FontColorHex) as System.Windows.Media.Brush; } catch { }
        TxtStage.Foreground = brush ?? System.Windows.Media.Brushes.White;

        double margin = w * 0.04;
        StageViewbox.Margin = new Thickness(margin);
        TxtStage.MaxWidth = Math.Max(200, w - 2 * margin);

        TxtStage.Text = settings.Uppercase ? _rawText.ToUpperInvariant() : _rawText;
    }
}
