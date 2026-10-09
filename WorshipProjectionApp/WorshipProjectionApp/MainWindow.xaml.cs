using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.Json;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;
using LayoutSettings = WorshipProjectionApp.Models.LayoutSettings;

namespace WorshipProjectionApp;

public partial class MainWindow : Window
{
    private static readonly string AutoSavePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave_schedule.json");
    private ProjectionSettings _projectionSettings = ProjectionSettings.Load();
    private LayoutSettings _layoutSettings = LayoutSettings.Load();

    public ObservableCollection<ScheduleItem> ScheduleItems { get; set; } = new();
    public ObservableCollection<Song> FilteredSongs { get; set; } = new();
    public ObservableCollection<WorshipTeamMember> TeamCatalog { get; set; } = new();
    public ObservableCollection<WorshipTeamMember> ActiveTeam { get; set; } = new();

    private ProjectionWindow? _projectionWindow;
    private string? _currentBackgroundPath;
    private bool _isBlackout;
    private readonly DatabaseService _dbService = new();
    private List<Song> _allSongs = new();
    private readonly BibleService _bibleService = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadSampleData();
        RestoreLastSession();
        LoadTeamData();

        LstSongs.ItemsSource = FilteredSongs;
        LstSchedule.ItemsSource = ScheduleItems;
        LstActiveTeam.ItemsSource = ActiveTeam;
        CmbPresetMembers.ItemsSource = TeamCatalog;
        if (TeamCatalog.Count > 0) CmbPresetMembers.SelectedIndex = 0;

        ApplyLayout(_layoutSettings);

        this.KeyDown += MainWindow_KeyDown;
    }

    private void LoadSampleData()
    {
        _allSongs = _dbService.GetAllSongs();

        if (_allSongs.Count == 0)
        {
            _dbService.InsertSong("Porque Ele Vive", "Bill Gaither",
                "[Verso 1]\nDeus enviou Seu Filho amado\nPara morrer no meu lugar\nNa cruz pagou por meus pecados\nMas o sepulcro vazio está\n\n[Coro]\nPorque Ele vive, posso crer no amanhã\nPorque Ele vive, temor não há\nMas eu bem sei, eu sei, que a minha vida\nEstá nas mãos do meu Jesus, que vivo está");

            _dbService.InsertSong("Grandioso És Tu", "Stuart K. Hine",
                "[Verso 1]\nSenhor meu Deus, quando eu maravilhado\nFico a pensar nas obras de Tuas mãos\nNo céu azul de estrelas pontilhado\nO Seu poder mostrando a criação\n\n[Coro]\nEntão a minha alma canta a Ti, Senhor\nGrandioso és Tu, grandioso és Tu!");

            _allSongs = _dbService.GetAllSongs();
        }

        UpdateSongFilter(string.Empty);
    }

    private void LoadTeamData()
    {
        var catalog = WorshipTeamCatalog.LoadCatalog();
        TeamCatalog.Clear();
        foreach (var c in catalog) TeamCatalog.Add(c);

        if (ActiveTeam.Count == 0 && TeamCatalog.Count >= 2)
        {
            ActiveTeam.Add(new WorshipTeamMember { Name = TeamCatalog[0].Name, Role = TeamCatalog[0].Role, Microphone = TeamCatalog[0].Microphone, InEarReturn = TeamCatalog[0].InEarReturn });
            ActiveTeam.Add(new WorshipTeamMember { Name = TeamCatalog[1].Name, Role = TeamCatalog[1].Role, Microphone = TeamCatalog[1].Microphone, InEarReturn = TeamCatalog[1].InEarReturn });
        }
    }

    private void BtnAddTeamMember_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPresetMembers.SelectedItem is WorshipTeamMember member)
        {
            if (!ActiveTeam.Any(m => m.Name.Equals(member.Name, StringComparison.OrdinalIgnoreCase)))
            {
                ActiveTeam.Add(new WorshipTeamMember
                {
                    Name = member.Name,
                    Microphone = member.Microphone,
                    InEarReturn = member.InEarReturn,
                    Role = member.Role
                });
            }
        }
    }

    private void BtnEditTeamMember_Click(object sender, RoutedEventArgs e)
    {
        // Previne que o evento suba e seja capturado pelo ListBox
        e.Handled = true;

        WorshipTeamMember? member = null;

        if (sender is FrameworkElement element)
        {
            member = element.DataContext as WorshipTeamMember;
        }

        if (member == null) return;

        var editDialog = new EditTeamMemberWindow(member)
        {
            Owner = this
        };

        if (editDialog.ShowDialog() == true)
        {
            if (editDialog.IsDeleted)
            {
                ActiveTeam.Remove(member);
            }
            else
            {
                int index = ActiveTeam.IndexOf(member);
                if (index >= 0)
                {
                    // Substitui a instância para notificar o ListBox da atualização
                    ActiveTeam[index] = new WorshipTeamMember
                    {
                        Name = editDialog.Member.Name,
                        Role = editDialog.Member.Role,
                        Microphone = editDialog.Member.Microphone,
                        InEarReturn = editDialog.Member.InEarReturn
                    };
                }
            }
        }
    }

    private void BtnOpenLayoutConfig_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new LayoutConfigWindow(_layoutSettings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _layoutSettings = dlg.ResultSettings;
            ApplyLayout(_layoutSettings);
        }
    }

    private void ApplyLayout(LayoutSettings settings)
    {
        if (MainColumnsGrid == null) return;

        var panelMap = new Dictionary<PanelType, FrameworkElement>
        {
            { PanelType.Resources,   PanelResources },
            { PanelType.Schedule,    PanelSchedule },
            { PanelType.Slides,      PanelSlides },
            { PanelType.Live,        PanelLive },
            { PanelType.WorshipTeam, PanelWorshipTeam }
        };

        foreach (var config in settings.Panels)
        {
            if (panelMap.TryGetValue(config.Type, out var element))
            {
                Grid.SetRow(element, Math.Clamp(config.Row, 0, 3));
                Grid.SetColumn(element, Math.Clamp(config.Column, 0, 3));
                Grid.SetRowSpan(element, Math.Clamp(config.RowSpan, 1, 4));
                Grid.SetColumnSpan(element, Math.Clamp(config.ColumnSpan, 1, 4));
            }
        }
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsDialog = new SettingsWindow(_projectionSettings)
        {
            Owner = this
        };

        if (settingsDialog.ShowDialog() == true)
        {
            _projectionSettings = settingsDialog.Settings;
            _projectionWindow?.ApplySettings(_projectionSettings);
        }
    }

    private void SaveCurrentSession()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(ScheduleItems, options);
            System.IO.File.WriteAllText(AutoSavePath, json);
        }
        catch { }
    }

    private void RestoreLastSession()
    {
        try
        {
            if (System.IO.File.Exists(AutoSavePath))
            {
                string json = System.IO.File.ReadAllText(AutoSavePath);
                var items = JsonSerializer.Deserialize<List<ScheduleItem>>(json);
                if (items != null && items.Count > 0)
                {
                    ScheduleItems.Clear();
                    foreach (var item in items)
                    {
                        ScheduleItems.Add(item);
                    }
                    LstSchedule.SelectedIndex = 0;
                }
            }
        }
        catch { }
    }

    private void TxtSearchBible_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            var results = _bibleService.Search(TxtSearchBible.Text);
            LstBibleResults.ItemsSource = results;
            if (results.Count > 0)
            {
                LstBibleResults.SelectedIndex = 0;
            }
        }
    }

    private void LstBibleResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBibleResults.SelectedItem is BibleVerse verse)
        {
            TxtSelectedSongTitle.Text = verse.Reference;

            var slide = verse.ToSlideItem();
            LstSlides.ItemsSource = new List<SlideItem> { slide };
            LstSlides.SelectedIndex = 0;

            SendToProjection(slide.Content);
        }
    }

    private void LstSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSchedule.SelectedItem is ScheduleItem item)
        {
            TxtSelectedSongTitle.Text = item.Title;
            LstSlides.ItemsSource = item.Slides;
            if (item.Slides.Count > 0)
            {
                LstSlides.SelectedIndex = 0;
            }
        }
    }

    private void BtnNextSlide_Click(object sender, RoutedEventArgs e)
    {
        NextSlide();
    }

    private void BtnPrevSlide_Click(object sender, RoutedEventArgs e)
    {
        PrevSlide();
    }

    private void NextSlide()
    {
        if (LstSlides.Items.Count == 0) return;

        if (LstSlides.SelectedIndex < LstSlides.Items.Count - 1)
        {
            LstSlides.SelectedIndex++;
        }
        else
        {
            if (LstSchedule.SelectedIndex < LstSchedule.Items.Count - 1)
            {
                LstSchedule.SelectedIndex++;
            }
        }
    }

    private void PrevSlide()
    {
        if (LstSlides.Items.Count == 0) return;

        if (LstSlides.SelectedIndex > 0)
        {
            LstSlides.SelectedIndex--;
        }
        else
        {
            if (LstSchedule.SelectedIndex > 0)
            {
                LstSchedule.SelectedIndex--;
                LstSlides.SelectedIndex = LstSlides.Items.Count - 1;
            }
        }
    }

    private void BtnImportEasyWorship_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar ficheiro Songs.db do EasyWorship",
            Filter = "Base de Dados EasyWorship (Songs.db)|Songs.db|Ficheiros DB (*.db)|*.db",
            InitialDirectory = @"C:\ProgramData\Softouch\Easyworship.v7\Databases\Data"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var imported = EasyWorshipImporter.ImportFromEasyWorshipSqlite(dialog.FileName);
                if (imported.Count > 0)
                {
                    _dbService.InsertBatch(imported);
                    _allSongs = _dbService.GetAllSongs();
                    UpdateSongFilter(string.Empty);

                    System.Windows.MessageBox.Show(
                        $"{imported.Count} músicas importadas com sucesso!",
                        "Importação Concluída",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Nenhuma música encontrada no ficheiro selecionado.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Erro ao importar: {ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void UpdateSongFilter(string query)
    {
        FilteredSongs.Clear();
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allSongs
            : _allSongs.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var song in matches) FilteredSongs.Add(song);
    }

    private void TxtSearchSong_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSongFilter(TxtSearchSong.Text);
    }

    private void LstSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSongs.SelectedItem is Song selectedSong)
        {
            TxtSelectedSongTitle.Text = selectedSong.Title;
            LstSlides.ItemsSource = selectedSong.ParseSlides();
            if (LstSlides.Items.Count > 0)
            {
                LstSlides.SelectedIndex = 0;
            }
        }
    }

    private void LstSlides_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSlides.SelectedItem is SlideItem slide)
        {
            SendToProjection(slide.Content);
        }
    }

    private void SendToProjection(string text)
    {
        TxtPreview.Text = text;

        if (_projectionWindow != null && !_isBlackout)
        {
            _projectionWindow.UpdateText(text);
        }
    }

    private void BtnToggleProjection_Click(object sender, RoutedEventArgs e)
    {
        if (_projectionWindow == null || !_projectionWindow.IsLoaded)
        {
            _projectionWindow = new ProjectionWindow();

            var screens = System.Windows.Forms.Screen.AllScreens;
            var targetScreen = screens.FirstOrDefault(s => !s.Primary) ?? screens.First();

            var bounds = targetScreen.Bounds;
            _projectionWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            _projectionWindow.Left = bounds.Left;
            _projectionWindow.Top = bounds.Top;
            _projectionWindow.Width = bounds.Width;
            _projectionWindow.Height = bounds.Height;

            if (screens.Length == 1)
            {
                _projectionWindow.WindowStyle = WindowStyle.SingleBorderWindow;
                _projectionWindow.ResizeMode = ResizeMode.CanResize;
                _projectionWindow.Width = 800;
                _projectionWindow.Height = 500;
            }
            else
            {
                _projectionWindow.WindowStyle = WindowStyle.None;
                _projectionWindow.ResizeMode = ResizeMode.NoResize;
                _projectionWindow.WindowState = WindowState.Maximized;
            }

            _projectionWindow.Show();
            _projectionWindow.ApplySettings(_projectionSettings);
            _projectionWindow.SetMediaBackground(_currentBackgroundPath);
            _projectionWindow.UpdateText(TxtPreview.Text);
        }
        else
        {
            _projectionWindow.Close();
            _projectionWindow = null;
        }
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;

        if (e.Key == Key.Escape)
        {
            BtnClearText_Click(sender, e);
        }
        else if (e.Key == Key.F6)
        {
            BtnBlackout_Click(sender, e);
        }
        else if (e.Key == Key.Down || e.Key == Key.Space || e.Key == Key.PageDown)
        {
            NextSlide();
            e.Handled = true;
        }
        else if (e.Key == Key.Up || e.Key == Key.PageUp)
        {
            PrevSlide();
            e.Handled = true;
        }
    }

    private void BtnClearText_Click(object sender, RoutedEventArgs e)
    {
        TxtPreview.Text = string.Empty;
        _projectionWindow?.ClearScreen();
    }

    private void BtnBlackout_Click(object sender, RoutedEventArgs e)
    {
        _isBlackout = !_isBlackout;

        if (_isBlackout)
        {
            SetPreviewMediaBackground(null);
            TxtPreview.Text = string.Empty;
            _projectionWindow?.SetBlackout(true);
        }
        else
        {
            SetPreviewMediaBackground(_currentBackgroundPath);
            if (LstSlides.SelectedItem is Models.SlideItem slide)
            {
                TxtPreview.Text = slide.Content;
            }

            if (_projectionWindow != null)
            {
                _projectionWindow.SetMediaBackground(_currentBackgroundPath);
                _projectionWindow.UpdateText(TxtPreview.Text);
            }
        }
    }

    private void SetPreviewMediaBackground(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            PreviewVideo.Stop();
            PreviewVideo.Source = null;
            PreviewBackgroundImage.Source = null;
            return;
        }

        string ext = System.IO.Path.GetExtension(filePath).ToLower();
        var videoExtensions = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };

        if (videoExtensions.Contains(ext))
        {
            PreviewBackgroundImage.Source = null;
            try
            {
                PreviewVideo.Source = new Uri(filePath, UriKind.Absolute);
                PreviewVideo.Position = TimeSpan.Zero;
                PreviewVideo.Play();
            }
            catch { }
        }
        else
        {
            PreviewVideo.Stop();
            PreviewVideo.Source = null;
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 320;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                PreviewBackgroundImage.Source = bitmap;
            }
            catch
            {
                PreviewBackgroundImage.Source = null;
            }
        }
    }

    private void PreviewVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        PreviewVideo.Position = TimeSpan.Zero;
        PreviewVideo.Play();
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = this.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveCurrentSession();
        _projectionWindow?.Close();
        base.OnClosed(e);
    }
}