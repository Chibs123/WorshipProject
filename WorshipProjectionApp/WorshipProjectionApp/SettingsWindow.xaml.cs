using System.Linq;
using System.Windows;
using System.Windows.Media;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;

namespace WorshipProjectionApp;

public partial class SettingsWindow : Window
{
    public ProjectionSettings Settings { get; private set; }

    private readonly Dictionary<string, System.Windows.Controls.Border> _themeCards = new();
    private readonly string _initialThemeId;
    private AppTheme _selectedTheme;
    private bool _saved;

    public SettingsWindow(ProjectionSettings currentSettings)
    {
        InitializeComponent();
        Settings = currentSettings;
        _initialThemeId = Settings.ThemeId;
        _selectedTheme = ThemeService.Get(Settings.ThemeId);
        BuildThemeCards();

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

        // Texto da projeção
        SldFontSize.Value = Math.Clamp(Settings.TextFontSize, 40, 160);
        SldLineSpacing.Value = Math.Clamp(Settings.LineSpacing, 1.0, 1.8);
        ChkUppercase.IsChecked = Settings.Uppercase;
        ChkOutline.IsChecked = Settings.OutlineEnabled;
        SldOutline.Value = Math.Clamp(Settings.OutlineThickness, 1, 10);
        ChkBackBox.IsChecked = Settings.BackBoxEnabled;
        SldBackBox.Value = Math.Clamp(Settings.BackBoxOpacity * 100, 10, 90);
        SldDimmer.Value = Math.Clamp(Settings.DimmerOpacity * 100, 0, 80);
        ChkBibleRefBottom.IsChecked = Settings.BibleRefAtBottom;
        SldMaxLines.Value = Math.Clamp(Settings.MaxLinesPerSlide, 0, 8);

        // Ecrã do louvor: 0 = desativado, 1.. = ecrãs detetados
        CmbStageScreen.Items.Add("Desativado");
        for (int i = 0; i < screens.Length; i++)
        {
            var sc = screens[i];
            CmbStageScreen.Items.Add($"Ecrã {i + 1}: {sc.Bounds.Width}x{sc.Bounds.Height}{(sc.Primary ? " (Ecrã Principal/Operador)" : "")}");
        }
        CmbStageScreen.SelectedIndex = (Settings.StageScreenIndex >= 0 && Settings.StageScreenIndex < screens.Length) ? Settings.StageScreenIndex + 1 : 0;
        SldStageFont.Value = Math.Clamp(Settings.StageFontSize, 50, 200);

        if (Settings.FontColorHex == "#FFF2A3") CmbTextColor.SelectedIndex = 1;
        else if (Settings.FontColorHex == "#E0F2FE") CmbTextColor.SelectedIndex = 2;
        else CmbTextColor.SelectedIndex = 0;
    }

    // ---------------------------------------------------------
    // TEMAS: passar o rato = pré-visualização em toda a app; clicar = escolher
    // ---------------------------------------------------------
    private void BuildThemeCards()
    {
        ThemePanel.Children.Clear();
        _themeCards.Clear();

        foreach (var theme in ThemeService.Themes)
        {
            var strip = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            foreach (var hex in new[] { theme.Canvas, theme.Card, theme.Border, theme.Accent, theme.Text })
            {
                strip.Children.Add(new System.Windows.Controls.Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 3, 0),
                    Background = new SolidColorBrush(ThemeService.ParseColor(hex)),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 128, 128, 128)),
                    BorderThickness = new Thickness(1)
                });
            }

            var textBrush = new SolidColorBrush(ThemeService.ParseColor(theme.Text));
            var mutedBrush = new SolidColorBrush(ThemeService.ParseColor(theme.Muted));

            var content = new System.Windows.Controls.StackPanel();
            content.Children.Add(strip);
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = theme.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = textBrush,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            content.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = theme.IsLight ? "Claro" : "Escuro",
                FontSize = 10,
                Foreground = mutedBrush
            });

            var card = new System.Windows.Controls.Border
            {
                Width = 124,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(ThemeService.ParseColor(theme.Panel)),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = content
            };
            card.ToolTip = theme.Description;

            var captured = theme;
            card.MouseEnter += (s, e) => ThemeService.Apply(captured);
            card.MouseLeave += (s, e) => ThemeService.Apply(_selectedTheme);
            card.MouseLeftButtonUp += (s, e) =>
            {
                _selectedTheme = captured;
                ThemeService.Apply(captured);
                UpdateThemeSelection();
            };

            _themeCards[theme.Id] = card;
            ThemePanel.Children.Add(card);
        }

        UpdateThemeSelection();
    }

    private void UpdateThemeSelection()
    {
        foreach (var theme in ThemeService.Themes)
        {
            bool selected = theme.Id == _selectedTheme.Id;
            _themeCards[theme.Id].BorderBrush = new SolidColorBrush(ThemeService.ParseColor(selected ? theme.Accent : theme.Border));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Se fechou sem guardar, volta ao tema que estava antes da pré-visualização
        if (!_saved) ThemeService.Apply(ThemeService.Get(_initialThemeId));
        base.OnClosed(e);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

    private void SldMargin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMarginVal != null) TxtMarginVal.Text = $"{(int)e.NewValue}%";
    }

    // Atualiza o texto do valor ao lado de cada slider novo (SldXxx -> TxtXxxVal)
    private void SldGeneric_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not System.Windows.Controls.Slider slider || slider.Name.Length < 4) return;
        if (FindName("Txt" + slider.Name.Substring(3) + "Val") is not System.Windows.Controls.TextBlock label) return;

        double v = e.NewValue;
        label.Text = slider.Name switch
        {
            "SldLineSpacing" => v.ToString("0.00"),
            "SldBackBox" or "SldDimmer" => $"{(int)v}%",
            "SldMaxLines" => v < 1 ? "Desligado" : $"{(int)v} linhas",
            _ => $"{(int)v}"
        };
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

        Settings.ThemeId = _selectedTheme.Id;
        _saved = true;
        Settings.TextFontSize = SldFontSize.Value;
        Settings.LineSpacing = Math.Round(SldLineSpacing.Value, 2);
        Settings.Uppercase = ChkUppercase.IsChecked == true;
        Settings.OutlineEnabled = ChkOutline.IsChecked == true;
        Settings.OutlineThickness = SldOutline.Value;
        Settings.BackBoxEnabled = ChkBackBox.IsChecked == true;
        Settings.BackBoxOpacity = SldBackBox.Value / 100.0;
        Settings.DimmerOpacity = SldDimmer.Value / 100.0;
        Settings.BibleRefAtBottom = ChkBibleRefBottom.IsChecked == true;
        Settings.MaxLinesPerSlide = (int)SldMaxLines.Value;
        Settings.StageScreenIndex = CmbStageScreen.SelectedIndex - 1; // -1 = desativado
        Settings.StageFontSize = SldStageFont.Value;

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