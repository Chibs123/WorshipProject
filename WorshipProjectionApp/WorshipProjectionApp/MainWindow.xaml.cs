using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using LayoutSettings = WorshipProjectionApp.Models.LayoutSettings;
using MessageBox = System.Windows.MessageBox;

namespace WorshipProjectionApp;

public partial class MainWindow : Window
{
    public ObservableCollection<MediaBackground> FilteredBackgrounds { get; set; } = new();
    private DragAdorner? _dragAdorner;
    private AdornerLayer? _adornerLayer;
    private static readonly string AutoSavePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave_schedule.json");
    private ProjectionSettings _projectionSettings = ProjectionSettings.Load();
    private LayoutSettings _layoutSettings = LayoutSettings.Load();

    public ObservableCollection<ScheduleItem> ScheduleItems { get; set; } = new();
    public ObservableCollection<Song> FilteredSongs { get; set; } = new();
    public ObservableCollection<WorshipTeamMember> TeamCatalog { get; set; } = new();
    public ObservableCollection<WorshipTeamMember> ActiveTeam { get; set; } = new();
    public ObservableCollection<MediaBackground> AvailableBackgrounds { get; set; } = new();
    public enum ProjectionMode { Logo, Clear, Live }
    private ProjectionMode _currentMode = ProjectionMode.Clear;
    private string? _logoFilePath;
    private ProjectionWindow? _projectionWindow;
    private string? _currentBackgroundPath;
    private bool _isBlackout;
    private readonly DatabaseService _dbService = new();
    private readonly BibleService _bibleService = new();
    private readonly BackgroundGalleryService _bgGalleryService = new();
    private List<Song> _allSongs = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadSampleData();
        RestoreLastSession();
        LoadTeamData();
        LoadBackgroundGallery();

        LstSongs.ItemsSource = FilteredSongs;
        LstSchedule.ItemsSource = ScheduleItems;
        LstActiveTeam.ItemsSource = ActiveTeam;
        LstBackgrounds.ItemsSource = AvailableBackgrounds;
        LstBackgrounds.ItemsSource = FilteredBackgrounds;

        CmbPresetMembers.ItemsSource = TeamCatalog;
        if (TeamCatalog.Count > 0) CmbPresetMembers.SelectedIndex = 0;

        ApplyLayout(_layoutSettings);
        LoadBibleVersions();
        this.KeyDown += MainWindow_KeyDown;
    }

    private void BtnImportBible_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar ficheiro(s) da Bíblia (SQLite / DB)",
            Filter = "Base de Dados SQLite (*.sqlite;*.db)|*.sqlite;*.db",
            Multiselect = true // <-- Permite selecionar vários ficheiros de uma vez!
        };

        if (dialog.ShowDialog() == true)
        {
            int successCount = 0;

            // Corre todos os ficheiros selecionados
            foreach (string file in dialog.FileNames)
            {
                if (_bibleService.ImportBible(file))
                {
                    successCount++;
                }
            }

            LoadBibleVersions();
            MessageBox.Show($"{successCount} Bíblias importadas com sucesso!", "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadBibleVersions()
    {
        var versions = _bibleService.GetAvailableVersions();
        CmbBibleVersion.ItemsSource = versions;

        if (versions.Count > 0)
            CmbBibleVersion.SelectedIndex = 0;
        else
            CmbBibleVersion.ItemsSource = new List<string> { "Nenhuma Bíblia" };
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

    private void BtnOpenAlerts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AlertWindow { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.ShouldHide)
            {
                _projectionWindow?.HideAlert();
            }
            else
            {
                _projectionWindow?.ShowAlert(dlg.MessageText, dlg.ShowClock, dlg.CountdownMinutes);
            }
        }
    }

    private void LoadBackgroundGallery()
    {
        AvailableBackgrounds.Clear();
        foreach (var bg in _bgGalleryService.GetBackgrounds())
        {
            AvailableBackgrounds.Add(bg);
        }
        ApplyBackgroundFilter();
    }

    // ---------------------------------------------------------
    // CRIAÇÃO E EDIÇÃO DE CANÇÕES
    // ---------------------------------------------------------
    private void BtnNewSong_Click(object sender, RoutedEventArgs e)
    {
        var editor = new SongEditorWindow { Owner = this };
        if (editor.ShowDialog() == true && editor.ResultSong != null)
        {
            _dbService.InsertSong(editor.ResultSong.Title, editor.ResultSong.Artist, editor.ResultSong.RawLyrics);
            _allSongs = _dbService.GetAllSongs();
            UpdateSongFilter(string.Empty);
        }
    }

    // ---------------------------------------------------------
    // DRAG & DROP (MÚSICAS, BÍBLIA E REORDENAÇÃO)
    // ---------------------------------------------------------
    private void LstSongs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && LstSongs.SelectedItem is Song selectedSong)
        {
            var item = new ScheduleItem
            {
                Title = selectedSong.Title,
                Subtitle = string.IsNullOrWhiteSpace(selectedSong.Artist) ? "Música" : selectedSong.Artist,
                Slides = selectedSong.ParseSlides()
            };

            StartDrag(LstSongs, item);
        }
    }



    private void ApplyBackgroundFilter()
    {
        FilteredBackgrounds.Clear();
        var list = AvailableBackgrounds.AsEnumerable();

        if (RbFilterVideos?.IsChecked == true)
        {
            list = list.Where(b => b.IsVideo);
        }
        else if (RbFilterImages?.IsChecked == true)
        {
            list = list.Where(b => !b.IsVideo);
        }

        foreach (var item in list)
        {
            FilteredBackgrounds.Add(item);
        }
    }

    private void BgFilter_Changed(object sender, RoutedEventArgs e)
    {
        ApplyBackgroundFilter();
    }

    private void LstBibleResults_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && LstBibleResults.SelectedItem is BibleVerse verse)
        {
            var item = new ScheduleItem
            {
                Title = verse.Reference,
                Subtitle = "Passagem Bíblica",
                Slides = new List<SlideItem> { verse.ToSlideItem() }
            };

            StartDrag(LstBibleResults, item);
        }
    }

    private void LstSchedule_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ScheduleItem)))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void LstSchedule_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ScheduleItem)) is ScheduleItem item)
        {
            // Se já existe na lista, move para o fim ou para o ponto de soltura
            if (ScheduleItems.Contains(item))
            {
                ScheduleItems.Remove(item);
            }

            ScheduleItems.Add(item);
            LstSchedule.SelectedItem = item;
            SaveCurrentSession();
        }
    }

    private void LstSchedule_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && LstSchedule.SelectedItem is ScheduleItem item)
        {
            DragDrop.DoDragDrop(LstSchedule, item, System.Windows.DragDropEffects.Move);
        }
    }

    // ---------------------------------------------------------
    // GALERIA DE FUNDOS VISUAIS
    // ---------------------------------------------------------
    private void BtnImportBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar Fundo do Windows (Vídeo ou Imagem)",
            Filter = "Ficheiros Multimédia|*.mp4;*.mov;*.wmv;*.avi;*.jpg;*.jpeg;*.png;*.webp;*.bmp|Vídeos (*.mp4;*.mov)|*.mp4;*.mov;*.wmv;*.avi|Imagens (*.jpg;*.png)|*.jpg;*.jpeg;*.png;*.webp;*.bmp",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (string file in dialog.FileNames)
            {
                var imported = _bgGalleryService.ImportFile(file);
                if (imported != null && !AvailableBackgrounds.Any(b => b.FilePath.Equals(imported.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableBackgrounds.Add(imported);
                }
            }
        }
    }

    private void LstBackgrounds_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            foreach (string file in files)
            {
                var imported = _bgGalleryService.ImportFile(file);
                if (imported != null && !AvailableBackgrounds.Any(b => b.FilePath.Equals(imported.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableBackgrounds.Add(imported);
                }
            }
        }
    }

    private void LstBackgrounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBackgrounds.SelectedItem is MediaBackground bg)
        {
            _currentBackgroundPath = bg.FilePath;
            SetPreviewMediaBackground(_currentBackgroundPath);

            // Se o logo estiver ativo, não mudamos o background do projetor!
            if (_projectionWindow != null && _currentMode != ProjectionMode.Logo)
            {
                _projectionWindow.SetMediaBackground(_currentBackgroundPath);
            }
        }
    }

    private void BtnClearBackground_Click(object sender, RoutedEventArgs e)
    {
        _currentBackgroundPath = null;
        LstBackgrounds.SelectedItem = null;
        SetPreviewMediaBackground(null);
        _projectionWindow?.SetMediaBackground(null);
    }

    // ---------------------------------------------------------
    // GRUPO DE LOUVOR
    // ---------------------------------------------------------
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
        e.Handled = true;

        WorshipTeamMember? member = null;

        if (sender is FrameworkElement element)
        {
            member = element.DataContext as WorshipTeamMember;
        }

        if (member == null) return;

        var editDialog = new EditTeamMemberWindow(member) { Owner = this };

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

    // ---------------------------------------------------------
    // GESTÃO DE LAYOUT LEGO
    // ---------------------------------------------------------
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

    private void BtnOpenBackgroundsFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string backgroundsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backgrounds");

            if (!System.IO.Directory.Exists(backgroundsFolder))
            {
                System.IO.Directory.CreateDirectory(backgroundsFolder);
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
            {
                FileName = backgroundsFolder,
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao abrir a pasta: {ex.Message}", "Erro");
        }
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_projectionSettings);
        if (settingsWindow.ShowDialog() == true)
        {
            _projectionSettings = settingsWindow.Settings;
            _projectionWindow?.ApplySettings(_projectionSettings);

            // Força a janela a saltar para o novo monitor se o utilizador trocou de ecrã!
            OpenProjectionWindow();
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
            string version = CmbBibleVersion.SelectedItem?.ToString() ?? "";

            if (version == "Nenhuma Bíblia" || string.IsNullOrEmpty(version))
                return;

            var results = _bibleService.Search(TxtSearchBible.Text, version);
            LstBibleResults.ItemsSource = results;
            if (results.Count > 0)
            {
                LstBibleResults.SelectedIndex = 0;
            }
        }
    }

    private void CmbBibleVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtSearchBible?.Text))
        {
            // Força a pesquisa novamente se mudares de ARC para NVI, por exemplo
            TxtSearchBible_KeyDown(TxtSearchBible, new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
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

        // Só envia texto se o modo estiver a LIVE
        if (_projectionWindow != null && _currentMode == ProjectionMode.Live)
        {
            _projectionWindow.UpdateText(text);
        }
    }

    // ---------------------------------------------------------
    // MÁQUINA DE ESTADOS DA PROJEÇÃO (LOGO, CLEAR, LIVE)
    // ---------------------------------------------------------
    private void BtnModeLogo_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Logo);
    private void BtnModeClear_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Clear);
    private void BtnModeLive_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Live);

    private void MenuSetLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar Ficheiro do Logo",
            Filter = "Imagens/Vídeos (*.jpg;*.png;*.mp4)|*.jpg;*.jpeg;*.png;*.mp4;*.mov"
        };
        if (dialog.ShowDialog() == true)
        {
            _logoFilePath = dialog.FileName;
            if (_currentMode == ProjectionMode.Logo)
            {
                SetProjectionMode(ProjectionMode.Logo); // Recarrega o logo se já estiver no modo Logo
            }
        }
    }

    private void SetProjectionMode(ProjectionMode mode)
    {
        OpenProjectionWindow(); // Garante que o projetor está ligado
        _currentMode = mode;

        // 1. Atualizar visual dos botões no topo (O ativo fica a Ouro, os outros a Azul Escuro)
        BtnModeLogo.Style = (Style)FindResource(mode == ProjectionMode.Logo ? "GoldButton" : "NavyButton");
        BtnModeClear.Style = (Style)FindResource(mode == ProjectionMode.Clear ? "GoldButton" : "NavyButton");
        BtnModeLive.Style = (Style)FindResource(mode == ProjectionMode.Live ? "GoldButton" : "NavyButton");

        // 2. Controlar Cores e Ações no Projetor e no Em Direto
        if (mode == ProjectionMode.Live)
        {
            // VERDE - Estamos a transmitir Letra + Fundo
            BadgeLiveDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            BadgeLiveText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(51, 16, 185, 129));
            BadgeLiveText.Text = "EM DIRETO";

            _projectionWindow?.SetBlackout(false);
            _projectionWindow?.SetMediaBackground(_currentBackgroundPath);
            _projectionWindow?.UpdateText(TxtPreview.Text);
        }
        else if (mode == ProjectionMode.Clear)
        {
            // VERMELHO - Fundo mantém-se, mas texto apagado
            BadgeLiveDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            BadgeLiveText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(51, 239, 68, 68));
            BadgeLiveText.Text = "SÓ FUNDO (CLEAR)";

            _projectionWindow?.SetBlackout(false);
            _projectionWindow?.SetMediaBackground(_currentBackgroundPath);
            _projectionWindow?.ClearScreen();
        }
        else if (mode == ProjectionMode.Logo)
        {
            // VERMELHO - Corta tudo e põe a imagem/vídeo do Logo
            BadgeLiveDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            BadgeLiveText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(51, 239, 68, 68));
            BadgeLiveText.Text = "LOGO ATIVO";

            _projectionWindow?.SetBlackout(false);
            _projectionWindow?.SetMediaBackground(_logoFilePath);
            _projectionWindow?.ClearScreen();
        }
    }

    private void OpenProjectionWindow()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        int savedScreenIndex = _projectionSettings.ScreenIndex;

        // 1. Descobre qual é o ecrã de destino atualizado
        var targetScreen = (savedScreenIndex >= 0 && savedScreenIndex < screens.Length)
                            ? screens[savedScreenIndex]
                            : (screens.FirstOrDefault(s => !s.Primary) ?? screens.First());

        var bounds = targetScreen.Bounds;

        // 2. A SOLUÇÃO ROBUSTA: Se a janela já existe mas está no ecrã errado,
        // temos de a fechar e reabrir. É a única forma 100% segura no WPF.
        if (_projectionWindow != null && _projectionWindow.IsLoaded)
        {
            // Usamos uma margem de tolerância (50px) para lidar com escalas do Windows (DPI)
            if (Math.Abs(_projectionWindow.Left - bounds.Left) > 50 ||
                Math.Abs(_projectionWindow.Top - bounds.Top) > 50)
            {
                _projectionWindow.Close();
                _projectionWindow = null; // Força a recriação da janela no monitor certo
            }
        }

        bool isNewWindow = (_projectionWindow == null);

        // 3. Cria e posiciona a janela (se for nova ou tiver sido fechada no passo anterior)
        if (isNewWindow)
        {
            _projectionWindow = new ProjectionWindow();
            _projectionWindow.WindowStartupLocation = WindowStartupLocation.Manual;

            // É obrigatório definir a posição ANTES de fazer o Show()
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
                // Em ecrãs duplos, garantimos que ela nasce "Normal" para o Windows a colocar 
                // nas coordenadas certas e só depois a maximizamos.
                _projectionWindow.WindowStyle = WindowStyle.None;
                _projectionWindow.ResizeMode = ResizeMode.NoResize;
                _projectionWindow.WindowState = WindowState.Normal;
            }

            _projectionWindow.Show();

            if (screens.Length > 1)
            {
                // Maximiza-se na perfeição no novo monitor
                _projectionWindow.WindowState = WindowState.Maximized;
            }
        }

        // 4. Aplica as novas definições (fontes, cores, sombras)
        _projectionWindow.ApplySettings(_projectionSettings);

        // 5. RESTAURA A TRANSMISSÃO (Para a música e o fundo não desaparecerem ao trocar de ecrã)
        if (isNewWindow)
        {
            if (_currentMode == ProjectionMode.Live)
            {
                _projectionWindow.SetBlackout(false);
                _projectionWindow.SetMediaBackground(_currentBackgroundPath);
                _projectionWindow.UpdateText(TxtPreview.Text);
            }
            else if (_currentMode == ProjectionMode.Clear)
            {
                _projectionWindow.SetBlackout(false);
                _projectionWindow.SetMediaBackground(_currentBackgroundPath);
                _projectionWindow.ClearScreen();
            }
            else if (_currentMode == ProjectionMode.Logo)
            {
                _projectionWindow.SetBlackout(false);
                _projectionWindow.SetMediaBackground(_logoFilePath);
                _projectionWindow.ClearScreen();
            }
        }
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;

        if (e.Key == Key.Escape)
        {
            SetProjectionMode(ProjectionMode.Clear);
            e.Handled = true;
        }
        else if (e.Key == Key.F6)
        {
            SetProjectionMode(ProjectionMode.Logo);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.F5)
        {
            SetProjectionMode(ProjectionMode.Live);
            e.Handled = true;
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

    private Border CreateDragVisual(string title, string subtitle)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 22, 32, 50)),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 175, 55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            IsHitTestVisible = false
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = "📄 " + title,
            FontWeight = FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 13
        });
        sp.Children.Add(new TextBlock
        {
            Text = subtitle,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
            FontSize = 11
        });

        border.Child = sp;
        return border;
    }

    // ---------------------------------------------------------
    // CONTROLOS DA PROGRAMAÇÃO (MOVER, REMOVER, LIMPAR)
    // ---------------------------------------------------------
    private void BtnScheduleMoveUp_Click(object sender, RoutedEventArgs e)
    {
        int index = LstSchedule.SelectedIndex;
        if (index > 0)
        {
            var item = ScheduleItems[index];
            ScheduleItems.RemoveAt(index);
            ScheduleItems.Insert(index - 1, item);
            LstSchedule.SelectedIndex = index - 1;
            SaveCurrentSession();
        }
    }

    private void BtnScheduleMoveDown_Click(object sender, RoutedEventArgs e)
    {
        int index = LstSchedule.SelectedIndex;
        if (index >= 0 && index < ScheduleItems.Count - 1)
        {
            var item = ScheduleItems[index];
            ScheduleItems.RemoveAt(index);
            ScheduleItems.Insert(index + 1, item);
            LstSchedule.SelectedIndex = index + 1;
            SaveCurrentSession();
        }
    }

    private void BtnScheduleRemove_Click(object sender, RoutedEventArgs e)
    {
        RemoveSelectedScheduleItem();
    }

    private void LstSchedule_KeyDown(object sender, KeyEventArgs e)
    {
        // Permite apagar a música da programação pressionando a tecla Delete ou Backspace
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            RemoveSelectedScheduleItem();
            e.Handled = true;
        }
    }

    private void RemoveSelectedScheduleItem()
    {
        int index = LstSchedule.SelectedIndex;
        if (index >= 0)
        {
            ScheduleItems.RemoveAt(index);
            SaveCurrentSession();

            // Seleciona automaticamente o item anterior ou o próximo para manter o fluxo
            if (ScheduleItems.Count > 0)
            {
                LstSchedule.SelectedIndex = Math.Min(index, ScheduleItems.Count - 1);
            }
            else
            {
                TxtSelectedSongTitle.Text = "Nenhum item selecionado";
                LstSlides.ItemsSource = null;
            }
        }
    }

    private void StartDrag(UIElement source, ScheduleItem item)
    {
        // 1. O truque: ancorar o Adorner ao conteúdo da janela (o Grid principal) e não à Window!
        UIElement rootElement = (UIElement)this.Content;
        _adornerLayer = AdornerLayer.GetAdornerLayer(rootElement);

        if (_adornerLayer != null)
        {
            var dragVisual = CreateDragVisual(item.Title, item.Subtitle);
            _dragAdorner = new DragAdorner(rootElement, dragVisual);
            _adornerLayer.Add(_dragAdorner);
        }

        // 2. Forçar a janela a rastrear o arrasto em toda a sua superfície
        this.AllowDrop = true;
        this.PreviewDragOver += MainWindow_PreviewDragOver;
        this.QueryContinueDrag += MainWindow_QueryContinueDrag;

        // 3. Inicia o bloqueio de Drag & Drop
        System.Windows.DragDrop.DoDragDrop(source, item, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);

        // 4. Limpeza quando o arrasto termina
        RemoveDragAdorner();
        this.AllowDrop = false; // Restaura o estado normal
    }

    private void MainWindow_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (_dragAdorner != null)
        {
            // Calcula a posição do rato relativa ao conteúdo interno
            var position = e.GetPosition((UIElement)this.Content);

            // O +15 serve para a etiqueta flutuante não ficar debaixo da ponta do rato, 
            // permitindo que o evento de largar seja detetado pela lista correta.
            _dragAdorner.UpdatePosition(new System.Windows.Point(position.X + 15, position.Y + 15));
        }
    }

    private void MainWindow_QueryContinueDrag(object sender, System.Windows.QueryContinueDragEventArgs e)
    {
        // Se a tecla ESC for premida ou o item for largado, limpamos a interface
        if (e.EscapePressed || e.Action == System.Windows.DragAction.Drop || e.Action == System.Windows.DragAction.Cancel)
        {
            RemoveDragAdorner();
        }
    }

    private void RemoveDragAdorner()
    {
        this.PreviewDragOver -= MainWindow_PreviewDragOver;
        this.QueryContinueDrag -= MainWindow_QueryContinueDrag;

        if (_dragAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_dragAdorner);
            _dragAdorner = null;
        }
    }
}