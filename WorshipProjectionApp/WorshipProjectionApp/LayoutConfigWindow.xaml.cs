using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorshipProjectionApp.Models;
using LayoutSettings = WorshipProjectionApp.Models.LayoutSettings;

namespace WorshipProjectionApp;

public partial class LayoutConfigWindow : Window
{
    public ObservableCollection<PanelConfig> LegoPanels { get; set; } = new();
    private bool _isUpdatingUi = false;
    public LayoutSettings ResultSettings { get; private set; } = new();

    public LayoutConfigWindow(LayoutSettings currentSettings)
    {
        InitializeComponent();

        foreach (var item in currentSettings.Panels)
        {
            LegoPanels.Add(new PanelConfig
            {
                Type = item.Type,
                Title = item.Title,
                Row = item.Row,
                Column = item.Column,
                RowSpan = item.RowSpan,
                ColumnSpan = item.ColumnSpan,
                IsVisible = item.IsVisible
            });
        }

        LstPieces.ItemsSource = LegoPanels;
        if (LegoPanels.Count > 0) LstPieces.SelectedIndex = 0;

        RenderPreview();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    private void LstPieces_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstPieces.SelectedItem is PanelConfig panel)
        {
            _isUpdatingUi = true;
            CmbRow.SelectedIndex = Math.Clamp(panel.Row, 0, 3);
            CmbCol.SelectedIndex = Math.Clamp(panel.Column, 0, 3);
            CmbColSpan.SelectedIndex = Math.Clamp(panel.ColumnSpan - 1, 0, 3);
            CmbRowSpan.SelectedIndex = Math.Clamp(panel.RowSpan - 1, 0, 3);
            _isUpdatingUi = false;
        }
    }

    private void ParamChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUi || LstPieces.SelectedItem is not PanelConfig panel) return;

        panel.Row = CmbRow.SelectedIndex;
        panel.Column = CmbCol.SelectedIndex;
        panel.ColumnSpan = CmbColSpan.SelectedIndex + 1;
        panel.RowSpan = CmbRowSpan.SelectedIndex + 1;

        // Limita a largura para não passar da coluna 3
        if (panel.Column + panel.ColumnSpan > 4)
        {
            panel.ColumnSpan = 4 - panel.Column;
            _isUpdatingUi = true;
            CmbColSpan.SelectedIndex = panel.ColumnSpan - 1;
            _isUpdatingUi = false;
        }

        // Limita a altura para não passar da linha 3
        if (panel.Row + panel.RowSpan > 4)
        {
            panel.RowSpan = 4 - panel.Row;
            _isUpdatingUi = true;
            CmbRowSpan.SelectedIndex = panel.RowSpan - 1;
            _isUpdatingUi = false;
        }

        RenderPreview();
    }

    private void RenderPreview()
    {
        if (GridPreview == null) return;
        GridPreview.Children.Clear();

        // Linhas de referência para as 16 células (4x4)
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 255, 255, 255)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(1)
                };
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                GridPreview.Children.Add(border);
            }
        }

        // Blocos Lego desenhados sobre a grelha
        foreach (var p in LegoPanels)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 32, 50)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 175, 55)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(3)
            };

            var txt = new TextBlock
            {
                Text = p.Title,
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            card.Child = txt;

            Grid.SetRow(card, p.Row);
            Grid.SetColumn(card, p.Column);
            Grid.SetRowSpan(card, p.RowSpan);
            Grid.SetColumnSpan(card, p.ColumnSpan);

            GridPreview.Children.Add(card);
        }
    }

    private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
    {
        var def = LayoutSettings.LoadDefault().Panels;
        LegoPanels.Clear();
        foreach (var p in def) LegoPanels.Add(p);
        LstPieces.SelectedIndex = 0;
        RenderPreview();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // 1. Força a atualização do item atualmente selecionado antes de fechar
        if (LstPieces.SelectedItem is PanelConfig currentPanel)
        {
            currentPanel.Row = CmbRow.SelectedIndex >= 0 ? CmbRow.SelectedIndex : currentPanel.Row;
            currentPanel.Column = CmbCol.SelectedIndex >= 0 ? CmbCol.SelectedIndex : currentPanel.Column;
            currentPanel.ColumnSpan = CmbColSpan.SelectedIndex >= 0 ? CmbColSpan.SelectedIndex + 1 : currentPanel.ColumnSpan;
            currentPanel.RowSpan = CmbRowSpan.SelectedIndex >= 0 ? CmbRowSpan.SelectedIndex + 1 : currentPanel.RowSpan;
        }

        // 2. Cria o objeto de configurações
        ResultSettings = new LayoutSettings
        {
            Panels = LegoPanels.ToList()
        };

        // 3. Grava no disco
        ResultSettings.Save();

        // 4. Retorna true para fechar o diálogo com sucesso
        this.DialogResult = true;
        this.Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}