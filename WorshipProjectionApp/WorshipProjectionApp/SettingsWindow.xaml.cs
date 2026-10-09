using System.Windows;
using System.Windows.Media;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp;

public partial class SettingsWindow : Window
{
    public ProjectionSettings Settings { get; private set; }

    public SettingsWindow(ProjectionSettings currentSettings)
    {
        InitializeComponent();
        Settings = currentSettings;

        // Preenche com fontes do sistema mais adequadas para projeção
        var standardFonts = new[] { "Segoe UI", "Arial", "Calibri", "Montserrat", "Trebuchet MS", "Verdana", "Impact" };
        var systemFonts = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();

        foreach (var font in standardFonts.Concat(systemFonts).Distinct())
        {
            CmbFontFamily.Items.Add(font);
        }

        // Carrega valores atuais
        CmbFontFamily.SelectedItem = CmbFontFamily.Items.Contains(Settings.FontFamily) ? Settings.FontFamily : "Segoe UI";
        ChkBold.IsChecked = Settings.IsBold;

        CmbAlignment.SelectedIndex = Settings.TextAlignment switch
        {
            TextAlignment.Left => 1,
            TextAlignment.Right => 2,
            _ => 0
        };

        CmbVerticalAlignment.SelectedIndex = Settings.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => 1,
            VerticalAlignment.Top => 2,
            _ => 0
        };

        SldMargin.Value = Settings.MarginPercent;
        SldShadow.Value = Settings.ShadowBlurRadius;

        if (Settings.FontColorHex == "#FFF2A3") CmbTextColor.SelectedIndex = 1;
        else if (Settings.FontColorHex == "#E0F2FE") CmbTextColor.SelectedIndex = 2;
        else CmbTextColor.SelectedIndex = 0;
    }

    private void SldMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMarginVal != null) TxtMarginVal.Text = $"{(int)e.NewValue}%";
    }

    private void SldShadow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtShadowVal != null) TxtShadowVal.Text = $"{(int)e.NewValue}";
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        Settings.FontFamily = CmbFontFamily.SelectedItem?.ToString() ?? "Segoe UI";
        Settings.IsBold = ChkBold.IsChecked == true;
        Settings.MarginPercent = (int)SldMargin.Value;
        Settings.ShadowBlurRadius = SldShadow.Value;

        Settings.TextAlignment = CmbAlignment.SelectedIndex switch
        {
            1 => TextAlignment.Left,
            2 => TextAlignment.Right,
            _ => TextAlignment.Center
        };

        Settings.VerticalAlignment = CmbVerticalAlignment.SelectedIndex switch
        {
            1 => VerticalAlignment.Bottom,
            2 => VerticalAlignment.Top,
            _ => VerticalAlignment.Center
        };

        Settings.FontColorHex = CmbTextColor.SelectedIndex switch
        {
            1 => "#FFF2A3",
            2 => "#E0F2FE",
            _ => "#FFFFFF"
        };

        Settings.Save();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}