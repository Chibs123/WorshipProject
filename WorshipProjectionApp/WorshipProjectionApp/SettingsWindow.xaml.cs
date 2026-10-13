using System.Linq;
using System.Windows;
using System.Windows.Media;
using WorshipProjectionApp.Models;
using Application = System.Windows.Application;

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
        var systemFonts = System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToList();
        var screens = System.Windows.Forms.Screen.AllScreens;

        foreach (var font in standardFonts.Concat(systemFonts).Distinct())
        {
            CmbFontFamily.Items.Add(font);
        }

        // Carregar Ecrãs Detetados
        CmbScreens.Items.Clear();
        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            string isPrimary = screen.Primary ? " (Ecrã Principal/Operador)" : "";
            CmbScreens.Items.Add($"Ecrã {i + 1}: {screen.Bounds.Width}x{screen.Bounds.Height}{isPrimary}");
        }

        // Aplica o ecrã guardado com proteção anti-crache
        if (Settings.ScreenIndex >= 0 && Settings.ScreenIndex < CmbScreens.Items.Count)
        {
            CmbScreens.SelectedIndex = Settings.ScreenIndex;
        }
        else
        {
            // Se nunca foi configurado, sugere o 2º ecrã automaticamente (se existir)
            CmbScreens.SelectedIndex = CmbScreens.Items.Count > 1 ? 1 : 0;
        }

        // Carrega valores atuais de estilo
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

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Background is SolidColorBrush brush)
        {
            // Muda a cor da app imediatamente!
            Application.Current.Resources["AccentColor"] = brush;

            // Guarda para não se esquecer quando fechar a janela
            Settings.AppThemeColorHex = brush.Color.ToString();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

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
        // Guardar Definições de Ecrã e Estilo
        Settings.ScreenIndex = CmbScreens.SelectedIndex;
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