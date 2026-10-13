using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using LayoutSettings = WorshipProjectionApp.Models.LayoutSettings;
using Point = System.Windows.Point;
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace WorshipProjectionApp;

public partial class MainWindow : Window
{
    public ObservableCollection<MediaBackground> FilteredBackgrounds { get; set; } = new();
    private DragAdorner? _dragAdorner;
    private AdornerLayer? _adornerLayer;
    private static readonly string AutoSavePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "autosave_schedule.json");
    private ProjectionSettings _projectionSettings = ProjectionSettings.Load();
    private LayoutSettings _layoutSettings = LayoutSettings.Load();
    private Point _scheduleDragStartPoint;
    private bool _isSchedulePotentialDrag;
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
    private Point _bibleDragStartPoint;
    private bool _isBiblePotentialDrag;
    private readonly DatabaseService _dbService = new();
    private readonly BibleService _bibleService = new();
    private readonly BackgroundGalleryService _bgGalleryService = new();
    private List<Song> _allSongs = new();

    private ScheduleItem? _itemToRename; // Guarda o item que está a ser renomeado

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
        LstBackgrounds.ItemsSource = FilteredBackgrounds;

        CmbPresetMembers.ItemsSource = TeamCatalog;
        if (TeamCatalog.Count > 0) CmbPresetMembers.SelectedIndex = 0;

        ApplyLayout(_layoutSettings);
        LoadBibleVersions();
        this.KeyDown += MainWindow_KeyDown;

        // --- CÓDIGO NOVO DO RELÓGIO AQUI ---
        var clockTimer = new System.Windows.Threading.DispatcherTimer();
        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (s, e) =>
        {
            TxtTopClock.Text = DateTime.Now.ToString("HH:mm:ss");
        };
        clockTimer.Start();

        // Atualiza logo no primeiro milissegundo para não mostrar "00:00"
        TxtTopClock.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void BtnImportBible_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar ficheiro(s) da Bíblia (SQLite / DB)",
            Filter = "Base de Dados SQLite (*.sqlite;*.db)|*.sqlite;*.db",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            int successCount = 0;
            foreach (string file in dialog.FileNames)
            {
                if (_bibleService.ImportBible(file)) successCount++;
            }
            LoadBibleVersions();
            MessageBox.Show($"{successCount} Bíblias importadas com sucesso!", "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void LoadBibleVersions()
    {
        var versions = _bibleService.GetAvailableVersions();
        CmbBibleVersion.ItemsSource = versions;

        if (versions.Count > 0) CmbBibleVersion.SelectedIndex = 0;
        else CmbBibleVersion.ItemsSource = new List<string> { "Nenhuma Bíblia" };
    }

    private void LoadSampleData()
    {
        _allSongs = _dbService.GetAllSongs();
        if (_allSongs.Count == 0)
        {
            _dbService.InsertSong("Porque Ele Vive", "Bill Gaither", "[Verso 1]\nDeus enviou Seu Filho amado\nPara morrer no meu lugar\nNa cruz pagou por meus pecados\nMas o sepulcro vazio está\n\n[Coro]\nPorque Ele vive, posso crer no amanhã");
            _dbService.InsertSong("Grandioso És Tu", "Stuart K. Hine", "[Verso 1]\nSenhor meu Deus, quando eu maravilhado\nFico a pensar nas obras de Tuas mãos\nNo céu azul de estrelas pontilhado\nO Seu poder mostrando a criação");
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
        }
    }

    private void BtnOpenAlerts_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AlertWindow { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.ShouldHide) _projectionWindow?.HideAlert();
            else _projectionWindow?.ShowAlert(dlg.MessageText, dlg.ShowClock, dlg.CountdownMinutes);
        }
    }

    private void LoadBackgroundGallery()
    {
        AvailableBackgrounds.Clear();
        foreach (var bg in _bgGalleryService.GetBackgrounds()) AvailableBackgrounds.Add(bg);
        ApplyBackgroundFilter();
    }

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

    private void BtnEditSong_Click(object sender, RoutedEventArgs e)
    {
        if (LstSongs.SelectedItem is Song selectedSong)
        {
            var editor = new SongEditorWindow(selectedSong) { Owner = this };
            if (editor.ShowDialog() == true && editor.ResultSong != null)
            {
                // A LINHA ABAIXO É A QUE ESTAVA A FALTAR PARA GRAVAR NA BASE DE DADOS!
                _dbService.UpdateSong(editor.ResultSong);

                _allSongs = _dbService.GetAllSongs();
                UpdateSongFilter(TxtSearchSong.Text);
            }
        }
    }

    // ---------------------------------------------------------
    // DRAG & DROP DAS MÚSICAS PARA A PROGRAMAÇÃO
    // ---------------------------------------------------------
    private void LstSongs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && LstSongs.SelectedItem is Song selectedSong)
        {
            var item = new ScheduleItem
            {
                Title = selectedSong.Title,
                Subtitle = string.IsNullOrWhiteSpace(selectedSong.Artist) ? "Música" : selectedSong.Artist,
                ItemTypeIcon = "🎵",
                Slides = selectedSong.ParseSlides()
            };
            StartDrag(LstSongs, item);
        }
    }

    private void ApplyBackgroundFilter()
    {
        FilteredBackgrounds.Clear();
        var list = AvailableBackgrounds.AsEnumerable();

        if (RbFilterVideos?.IsChecked == true) list = list.Where(b => b.IsVideo);
        else if (RbFilterImages?.IsChecked == true) list = list.Where(b => !b.IsVideo);

        foreach (var item in list) FilteredBackgrounds.Add(item);
    }

    private void BgFilter_Changed(object sender, RoutedEventArgs e) => ApplyBackgroundFilter();

    // ---------------------------------------------------------
    // EVENTOS DE DRAG & DROP DA PROGRAMAÇÃO (SCHEDULE)
    // ---------------------------------------------------------
    private void LstSchedule_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ScheduleItem))) e.Effects = System.Windows.DragDropEffects.Move;
        else e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void LstSchedule_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Grava a posição exata onde o clique começou
        _scheduleDragStartPoint = e.GetPosition(null);
        _isSchedulePotentialDrag = true;
    }

    private void LstSchedule_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _isSchedulePotentialDrag && LstSchedule.SelectedItem is ScheduleItem item)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _scheduleDragStartPoint - mousePos;

            // Só ativa o arrasto se o rato se mover uma distância real (evita falsos arrastos no clique)
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isSchedulePotentialDrag = false;
                DragDrop.DoDragDrop(LstSchedule, item, System.Windows.DragDropEffects.Move);
            }
        }
    }

    private void LstSchedule_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isSchedulePotentialDrag = false;
    }

    private void LstSchedule_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ScheduleItem)) is ScheduleItem item)
        {
            int removeIndex = ScheduleItems.IndexOf(item);
            int dropIndex = -1;

            // Descobre em cima de que item o operador largou o cartão para inserir no local correto
            if (e.OriginalSource is FrameworkElement fe && fe.DataContext is ScheduleItem targetItem)
            {
                dropIndex = ScheduleItems.IndexOf(targetItem);
            }

            if (removeIndex != -1) // Reordenação (vem da própria lista)
            {
                // O WPF tem um método perfeito para isto (Move), garante que não há duplicações!
                if (dropIndex != -1 && dropIndex != removeIndex)
                {
                    ScheduleItems.Move(removeIndex, dropIndex);
                }
                else if (dropIndex == -1) // Se largou no fundo vazio da lista
                {
                    ScheduleItems.Move(removeIndex, ScheduleItems.Count - 1);
                }
            }
            else // Novo Item (vem das Músicas ou Bíblia)
            {
                if (dropIndex != -1) ScheduleItems.Insert(dropIndex, item);
                else ScheduleItems.Add(item);
            }

            LstSchedule.SelectedItem = item;
            SaveCurrentSession();
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
            bool addedNew = false;
            foreach (string file in dialog.FileNames)
            {
                var imported = _bgGalleryService.ImportFile(file);
                if (imported != null && !AvailableBackgrounds.Any(b => b.FilePath.Equals(imported.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableBackgrounds.Add(imported);
                    addedNew = true;
                }
            }
            if (addedNew) ApplyBackgroundFilter();
        }
    }

    private void LstBackgrounds_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            bool addedNew = false;

            foreach (string file in files)
            {
                var imported = _bgGalleryService.ImportFile(file);
                if (imported != null && !AvailableBackgrounds.Any(b => b.FilePath.Equals(imported.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    AvailableBackgrounds.Add(imported);
                    addedNew = true;
                }
            }
            if (addedNew) ApplyBackgroundFilter();
        }
    }

    private void BackgroundItem_MouseEnter(object sender, MouseEventArgs e)
    {
        // HOVER PREVIEW: Mostra no ecrã de preview interno (mas não no Live)
        if (sender is FrameworkElement el && el.DataContext is MediaBackground bg)
        {
            SetPreviewMediaBackground(bg.FilePath);
        }
    }

    private void BackgroundItem_MouseLeave(object sender, MouseEventArgs e)
    {
        // Restaura a miniatura do fundo que está efetivamente selecionado
        SetPreviewMediaBackground(_currentBackgroundPath);
    }

    private void LstBackgrounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBackgrounds.SelectedItem is MediaBackground bg)
        {
            _currentBackgroundPath = bg.FilePath;
            SetPreviewMediaBackground(_currentBackgroundPath);

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
                ActiveTeam.Add(new WorshipTeamMember { Name = member.Name, Microphone = member.Microphone, InEarReturn = member.InEarReturn, Role = member.Role });
            }
        }
    }

    private void BtnEditTeamMember_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        WorshipTeamMember? member = null;
        if (sender is FrameworkElement element) member = element.DataContext as WorshipTeamMember;
        if (member == null) return;

        var editDialog = new EditTeamMemberWindow(member) { Owner = this };
        if (editDialog.ShowDialog() == true)
        {
            if (editDialog.IsDeleted) ActiveTeam.Remove(member);
            else
            {
                int index = ActiveTeam.IndexOf(member);
                if (index >= 0) ActiveTeam[index] = new WorshipTeamMember { Name = editDialog.Member.Name, Role = editDialog.Member.Role, Microphone = editDialog.Member.Microphone, InEarReturn = editDialog.Member.InEarReturn };
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

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_projectionSettings) { Owner = this };
        if (settingsWindow.ShowDialog() == true)
        {
            _projectionSettings = settingsWindow.Settings;
            _projectionWindow?.ApplySettings(_projectionSettings);
            OpenProjectionWindow();
        }
    }

    private void SaveCurrentSession()
    {
        try { File.WriteAllText(AutoSavePath, JsonSerializer.Serialize(ScheduleItems, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }

    private void RestoreLastSession()
    {
        try
        {
            if (File.Exists(AutoSavePath))
            {
                var items = JsonSerializer.Deserialize<List<ScheduleItem>>(File.ReadAllText(AutoSavePath));
                if (items != null && items.Count > 0)
                {
                    ScheduleItems.Clear();
                    foreach (var item in items) ScheduleItems.Add(item);
                    LstSchedule.SelectedIndex = 0;
                }
            }
        }
        catch { }
    }

    // ---------------------------------------------------------
    // BÍBLIA INTELIGENTE
    // ---------------------------------------------------------
    private void TxtSearchBible_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            string version = CmbBibleVersion.SelectedItem?.ToString() ?? "";
            if (version == "Nenhuma Bíblia" || string.IsNullOrEmpty(version)) return;

            var results = _bibleService.Search(TxtSearchBible.Text, version);
            LstBibleResults.ItemsSource = results;

            if (results.Count > 0)
            {
                if (TxtSearchBible.Text.Contains("-")) LstBibleResults.SelectAll();
                else LstBibleResults.SelectedIndex = 0;

                // Foco Automático para não precisares de clicar com o rato
                Keyboard.ClearFocus();
                LstBibleResults.Focus();
            }
        }
    }

    private void LstBibleResults_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _bibleDragStartPoint = e.GetPosition(null);
        var item = ItemsControl.ContainerFromElement(LstBibleResults, e.OriginalSource as DependencyObject) as ListBoxItem;

        if (item != null && item.IsSelected)
        {
            _isBiblePotentialDrag = true;
            e.Handled = true;
        }
        else _isBiblePotentialDrag = false;
    }

    private void LstBibleResults_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _isBiblePotentialDrag && LstBibleResults.SelectedItems.Count > 0)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _bibleDragStartPoint - mousePos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isBiblePotentialDrag = false;
                var selectedVerses = LstBibleResults.SelectedItems.Cast<BibleVerse>().ToList();

                if (selectedVerses.Count == 1)
                {
                    var verse = selectedVerses[0];
                    var item = new ScheduleItem
                    {
                        Title = verse.Reference,
                        Subtitle = "Passagem Bíblica",
                        ItemTypeIcon = "📖",
                        Slides = new List<SlideItem> { verse.ToSlideItem() }
                    };
                    StartDrag(LstBibleResults, item);
                }
                else
                {
                    var firstVerse = selectedVerses.First();
                    var lastVerse = selectedVerses.Last();
                    string titleRange = $"{firstVerse.Reference} a {lastVerse.Reference}";

                    var slides = new List<SlideItem>();
                    foreach (var verse in selectedVerses) slides.Add(verse.ToSlideItem());

                    var item = new ScheduleItem
                    {
                        Title = titleRange,
                        Subtitle = $"Leitura Bíblica ({selectedVerses.Count} versículos)",
                        ItemTypeIcon = "📖",
                        Slides = slides
                    };
                    StartDrag(LstBibleResults, item);
                }
            }
        }
    }

    private void LstBibleResults_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isBiblePotentialDrag)
        {
            _isBiblePotentialDrag = false;
            var item = ItemsControl.ContainerFromElement(LstBibleResults, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                LstBibleResults.SelectedItems.Clear();
                item.IsSelected = true;
            }
        }
    }

    private void CmbBibleVersion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtSearchBible?.Text))
        {
            TxtSearchBible_KeyDown(TxtSearchBible, new System.Windows.Input.KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent });
        }
    }

    private void LstBibleResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBibleResults.SelectedItems.Count > 0 && LstBibleResults.SelectedItems[0] is BibleVerse verse)
        {
            TxtSelectedSongTitle.Text = verse.Reference;
            var slide = verse.ToSlideItem();
            LstSlides.ItemsSource = new List<SlideItem> { slide };
            LstSlides.SelectedIndex = 0;
            SendToProjection(slide.Content);
        }
    }

    // ---------------------------------------------------------
    // LÓGICA DE APRESENTAÇÃO E "LIVE"
    // ---------------------------------------------------------
    private void LstSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSchedule.SelectedItem is ScheduleItem item)
        {
            TxtSelectedSongTitle.Text = item.Title;
            LstSlides.ItemsSource = item.Slides;
            if (item.Slides.Count > 0) LstSlides.SelectedIndex = 0;

            // Borda Ativa Dinâmica
            foreach (var sch in ScheduleItems) sch.IsActive = (sch == item);
        }
    }

    private void LstSchedule_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Duplo clique entra em Modo LIVE imediatamente
        if (LstSchedule.SelectedItem is ScheduleItem)
        {
            SetProjectionMode(ProjectionMode.Live);
        }
    }

    // ---------------------------------------------------------
    // GRAVAR, CARREGAR, LIMPAR E RENOMEAR O CULTO
    // ---------------------------------------------------------
    private void MenuRenameScheduleItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ScheduleItem item)
        {
            _itemToRename = item;
            TxtRenameItem.Text = item.Title;
            OverlayRename.Visibility = Visibility.Visible;
            TxtRenameItem.Focus();
            TxtRenameItem.SelectAll();
        }
    }

    private void BtnSaveRename_Click(object sender, RoutedEventArgs e)
    {
        if (_itemToRename != null && !string.IsNullOrWhiteSpace(TxtRenameItem.Text))
        {
            _itemToRename.Title = TxtRenameItem.Text;
            SaveCurrentSession();
        }
        OverlayRename.Visibility = Visibility.Collapsed;
        _itemToRename = null;
    }

    private void BtnCancelRename_Click(object sender, RoutedEventArgs e)
    {
        OverlayRename.Visibility = Visibility.Collapsed;
        _itemToRename = null;
    }

    private void TxtRenameItem_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnSaveRename_Click(sender, e);
        else if (e.Key == Key.Escape) BtnCancelRename_Click(sender, e);
    }

    private void BtnScheduleClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Tem a certeza que deseja limpar toda a programação do culto?", "Limpar Culto", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            ScheduleItems.Clear();
            LstSlides.ItemsSource = null;
            TxtSelectedSongTitle.Text = "Nenhum item selecionado";
            SaveCurrentSession();
        }
    }

    private void BtnSavePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Gravar Culto", Filter = "Playlist (*.json)|*.json", FileName = "Culto_Domingo.json" };
        if (dialog.ShowDialog() == true)
        {
            try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(ScheduleItems, new JsonSerializerOptions { WriteIndented = true })); } catch { }
        }
    }

    private void BtnLoadPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Abrir Culto", Filter = "Playlist (*.json)|*.json" };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<ScheduleItem>>(File.ReadAllText(dialog.FileName));
                if (items != null)
                {
                    ScheduleItems.Clear();
                    foreach (var item in items) ScheduleItems.Add(item);
                }
            }
            catch { MessageBox.Show("Erro ao carregar o culto."); }
        }
    }

    // ---------------------------------------------------------
    // OUTRAS FUNÇÕES DA PROGRAMAÇÃO E SLIDES
    // ---------------------------------------------------------
    private void BtnScheduleMoveUp_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstSchedule.SelectedIndex;
        if (idx > 0) { var i = ScheduleItems[idx]; ScheduleItems.RemoveAt(idx); ScheduleItems.Insert(idx - 1, i); LstSchedule.SelectedIndex = idx - 1; SaveCurrentSession(); }
    }

    private void BtnScheduleMoveDown_Click(object sender, RoutedEventArgs e)
    {
        int idx = LstSchedule.SelectedIndex;
        if (idx >= 0 && idx < ScheduleItems.Count - 1) { var i = ScheduleItems[idx]; ScheduleItems.RemoveAt(idx); ScheduleItems.Insert(idx + 1, i); LstSchedule.SelectedIndex = idx + 1; SaveCurrentSession(); }
    }

    private void BtnScheduleRemove_Click(object sender, RoutedEventArgs e) { RemoveSelectedScheduleItem(); }

    private void LstSchedule_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Delete || e.Key == Key.Back) { RemoveSelectedScheduleItem(); e.Handled = true; } }

    private void RemoveSelectedScheduleItem()
    {
        int idx = LstSchedule.SelectedIndex;
        if (idx >= 0)
        {
            ScheduleItems.RemoveAt(idx);
            SaveCurrentSession();
            if (ScheduleItems.Count > 0) LstSchedule.SelectedIndex = Math.Min(idx, ScheduleItems.Count - 1);
            else { TxtSelectedSongTitle.Text = "Nenhum item selecionado"; LstSlides.ItemsSource = null; }
        }
    }

    private void UpdateSongFilter(string query)
    {
        FilteredSongs.Clear();
        var matches = string.IsNullOrWhiteSpace(query) ? _allSongs : _allSongs.Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var song in matches) FilteredSongs.Add(song);
    }

    private void TxtSearchSong_TextChanged(object sender, TextChangedEventArgs e) => UpdateSongFilter(TxtSearchSong.Text);

    private void LstSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSongs.SelectedItem is Song selectedSong)
        {
            TxtSelectedSongTitle.Text = selectedSong.Title;
            LstSlides.ItemsSource = selectedSong.ParseSlides();
            if (LstSlides.Items.Count > 0) LstSlides.SelectedIndex = 0;
        }
    }

    private void LstSlides_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSlides.SelectedItem is SlideItem slide)
        {
            // 1. Descobre se o Slide tem um fundo próprio ou se usa o fundo global do Culto
            string? activeBg = slide.HasCustomBackground ? slide.BackgroundPath : _currentBackgroundPath;

            // 2. Atualiza a miniatura do ecrã "Em Direto" (na interface do operador)
            SetPreviewMediaBackground(activeBg);

            // 3. Atualiza o projetor real (se não estiver em modo Logo)
            if (_projectionWindow != null && _currentMode != ProjectionMode.Logo)
            {
                _projectionWindow.SetMediaBackground(activeBg);
            }

            // 4. Manda o texto do slide
            SendToProjection(slide.Content);
        }
    }

    private void SendToProjection(string text)
    {
        TxtPreview.Text = text;
        if (_projectionWindow != null && _currentMode == ProjectionMode.Live) _projectionWindow.UpdateText(text);
    }

    // ---------------------------------------------------------
    // MÁQUINA DE ESTADOS DA PROJEÇÃO (LOGO, CLEAR, LIVE)
    // ---------------------------------------------------------
    private void BtnModeLogo_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Logo);
    private void BtnModeClear_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Clear);
    private void BtnModeLive_Click(object sender, RoutedEventArgs e) => SetProjectionMode(ProjectionMode.Live);

    private void MenuSetLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Selecionar Ficheiro do Logo", Filter = "Imagens/Vídeos (*.jpg;*.png;*.mp4)|*.jpg;*.jpeg;*.png;*.mp4;*.mov" };
        if (dialog.ShowDialog() == true)
        {
            _logoFilePath = dialog.FileName;
            if (_currentMode == ProjectionMode.Logo) SetProjectionMode(ProjectionMode.Logo);
        }
    }

    private void SetProjectionMode(ProjectionMode mode)
    {
        OpenProjectionWindow();
        _currentMode = mode;

        BtnModeLogo.Style = (Style)FindResource(mode == ProjectionMode.Logo ? "GoldButton" : "NavyButton");
        BtnModeClear.Style = (Style)FindResource(mode == ProjectionMode.Clear ? "GoldButton" : "NavyButton");
        BtnModeLive.Style = (Style)FindResource(mode == ProjectionMode.Live ? "GoldButton" : "NavyButton");

        if (mode == ProjectionMode.Live)
        {
            BadgeLiveDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            BadgeLiveText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 16, 185, 129));
            BadgeLiveText.Text = "EM DIRETO";
            _projectionWindow?.SetBlackout(false);
            _projectionWindow?.SetMediaBackground(_currentBackgroundPath);
            _projectionWindow?.UpdateText(TxtPreview.Text);
        }
        else if (mode == ProjectionMode.Clear)
        {
            BadgeLiveDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BadgeLiveText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 239, 68, 68));
            BadgeLiveText.Text = "SÓ FUNDO (CLEAR)";
            _projectionWindow?.SetBlackout(false);
            _projectionWindow?.SetMediaBackground(_currentBackgroundPath);
            _projectionWindow?.ClearScreen();
        }
        else if (mode == ProjectionMode.Logo)
        {
            BadgeLiveDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BadgeLiveText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BadgeLiveBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, 239, 68, 68));
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
        var targetScreen = (savedScreenIndex >= 0 && savedScreenIndex < screens.Length) ? screens[savedScreenIndex] : (screens.FirstOrDefault(s => !s.Primary) ?? screens.First());
        var bounds = targetScreen.Bounds;

        if (_projectionWindow != null && _projectionWindow.IsLoaded)
        {
            if (Math.Abs(_projectionWindow.Left - bounds.Left) > 50 || Math.Abs(_projectionWindow.Top - bounds.Top) > 50)
            {
                _projectionWindow.Close();
                _projectionWindow = null;
            }
        }

        bool isNewWindow = (_projectionWindow == null);

        if (isNewWindow)
        {
            _projectionWindow = new ProjectionWindow();
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
                _projectionWindow.WindowState = WindowState.Normal;
            }

            _projectionWindow.Show();
            if (screens.Length > 1) _projectionWindow.WindowState = WindowState.Maximized;
        }

        _projectionWindow.ApplySettings(_projectionSettings);

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

        if (e.Key == Key.Escape) { SetProjectionMode(ProjectionMode.Clear); e.Handled = true; }
        else if (e.Key == Key.F6) { SetProjectionMode(ProjectionMode.Logo); e.Handled = true; }
        else if (e.Key == System.Windows.Input.Key.F5) { SetProjectionMode(ProjectionMode.Live); e.Handled = true; }
        else if (e.Key == Key.Down || e.Key == Key.Space || e.Key == Key.PageDown) { NextSlide(); e.Handled = true; }
        else if (e.Key == Key.Up || e.Key == Key.PageUp) { PrevSlide(); e.Handled = true; }
    }

    private void NextSlide()
    {
        if (LstSlides.Items.Count == 0) return;
        if (LstSlides.SelectedIndex < LstSlides.Items.Count - 1) LstSlides.SelectedIndex++;
        else if (LstSchedule.SelectedIndex < LstSchedule.Items.Count - 1) LstSchedule.SelectedIndex++;
    }

    private void PrevSlide()
    {
        if (LstSlides.Items.Count == 0) return;
        if (LstSlides.SelectedIndex > 0) LstSlides.SelectedIndex--;
        else if (LstSchedule.SelectedIndex > 0) { LstSchedule.SelectedIndex--; LstSlides.SelectedIndex = LstSlides.Items.Count - 1; }
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
                bitmap.Freeze();
                PreviewBackgroundImage.Source = bitmap;
            }
            catch { PreviewBackgroundImage.Source = null; }
        }
    }

    private void PreviewVideo_MediaEnded(object sender, RoutedEventArgs e) { PreviewVideo.Position = TimeSpan.Zero; PreviewVideo.Play(); }
    private void BtnMinimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void BtnClose_Click(object sender, RoutedEventArgs e) { this.Close(); }
    protected override void OnClosed(EventArgs e) { SaveCurrentSession(); _projectionWindow?.Close(); base.OnClosed(e); }

    // ---------------------------------------------------------
    // DRAG ADORNER (EFEITO VISUAL AO ARRASTAR DE FORA DA PROGRAMAÇÃO)
    // ---------------------------------------------------------
    private void StartDrag(UIElement source, ScheduleItem item)
    {
        UIElement rootElement = (UIElement)this.Content;
        _adornerLayer = AdornerLayer.GetAdornerLayer(rootElement);

        if (_adornerLayer != null)
        {
            var dragVisual = CreateDragVisual(item.Title, item.Subtitle);
            _dragAdorner = new DragAdorner(rootElement, dragVisual);
            _adornerLayer.Add(_dragAdorner);
        }

        this.AllowDrop = true;
        this.PreviewDragOver += MainWindow_PreviewDragOver;
        this.QueryContinueDrag += MainWindow_QueryContinueDrag;

        System.Windows.DragDrop.DoDragDrop(source, item, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);

        RemoveDragAdorner();
        this.AllowDrop = false;
    }

    private void MainWindow_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (_dragAdorner != null)
        {
            var position = e.GetPosition((UIElement)this.Content);
            _dragAdorner.UpdatePosition(new System.Windows.Point(position.X + 15, position.Y + 15));
        }
    }

    private void MainWindow_QueryContinueDrag(object sender, System.Windows.QueryContinueDragEventArgs e)
    {
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

    private Border CreateDragVisual(string title, string subtitle)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 22, 32, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(212, 175, 55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            IsHitTestVisible = false
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "📄 " + title, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, FontSize = 13 });
        sp.Children.Add(new TextBlock { Text = subtitle, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 11 });
        border.Child = sp;
        return border;
    }

    private void BtnImportEasyWorship_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Selecionar ficheiro Songs.db", Filter = "Base de Dados EasyWorship (Songs.db)|Songs.db" };
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
                    MessageBox.Show($"{imported.Count} músicas importadas!");
                }
            }
            catch { }
        }
    }
}