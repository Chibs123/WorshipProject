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
using DragEventArgs = System.Windows.DragEventArgs;
using ListBox = System.Windows.Controls.ListBox;
using DragDropEffects = System.Windows.DragDropEffects;
using Button = System.Windows.Controls.Button;

namespace WorshipProjectionApp;

public partial class MainWindow : Window
{
    private string? _lastLoadedPreviewPath;
    private System.Windows.Threading.DispatcherTimer? _hoverPreviewTimer;
    private string? _pendingHoverPath;
    public ObservableCollection<MediaBackground> FilteredBackgrounds { get; set; } = new();
    private DragAdorner? _dragAdorner;
    private AdornerLayer? _adornerLayer;
    private static readonly string AutoSavePath = AppPaths.File("autosave_schedule.json");
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
    private readonly PowerPointService _powerPointService = new PowerPointService();

    private ProjectionWindow? _projectionWindow;
    private string? _currentBackgroundPath;
    private string? _projectorBgPath;      // último fundo enviado ao projetor
    private string _currentSlideText = string.Empty; // texto do slide selecionado (só vai para o ecrã em modo LIVE)
    private string _currentReference = string.Empty; // referência bíblica do slide (se houver)
    private StageWindow? _stageWindow;               // ecrã do grupo de louvor
    private Point _bibleDragStartPoint;
    private bool _isBiblePotentialDrag;
    private readonly DatabaseService _dbService = new();
    private readonly BibleService _bibleService = new();
    private readonly BackgroundGalleryService _bgGalleryService = new();
    private List<Song> _allSongs = new();

    private ScheduleItem? _itemToRename; // Guarda o item que está a ser renomeado
    private Point _songDragStartPoint;
    private System.Windows.Threading.DispatcherTimer? _saveTimer;
    private System.Windows.Threading.DispatcherTimer? _songSearchTimer;

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
        LstSongs.PreviewMouseLeftButtonDown += (s, e) => _songDragStartPoint = e.GetPosition(null);

        CmbPresetMembers.ItemsSource = TeamCatalog;
        if (TeamCatalog.Count > 0) CmbPresetMembers.SelectedIndex = 0;

        ApplyLayout(_layoutSettings);
        LoadBibleVersions();
        this.KeyDown += MainWindow_KeyDown;
        this.PreviewKeyDown += MainWindow_PreviewKeyDown;
        StartRemote();

        // --- CÓDIGO NOVO DO RELÓGIO AQUI ---
        var clockTimer = new System.Windows.Threading.DispatcherTimer();
        clockTimer.Interval = TimeSpan.FromSeconds(1);
        clockTimer.Tick += (s, e) =>
        {
            TxtTopClock.Text = DateTime.Now.ToString("HH:mm:ss");
            SyncAlertPreview();
        };
        clockTimer.Start();

        // Atualiza logo no primeiro milissegundo para não mostrar "00:00"
        TxtTopClock.Text = DateTime.Now.ToString("HH:mm:ss");
        ApplyBadge(_currentMode);
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

    // Nota: Adicionei a palavra "async" aqui
    private async void BtnImportPowerPoint_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Apresentações PowerPoint (*.pptx;*.ppt)|*.pptx;*.ppt",
            Title = "Selecione o ficheiro do PowerPoint"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            // 1. Muda o cursor para "A carregar..." para o operador perceber que a app está a trabalhar
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            try
            {
                // 2. MAGIA AQUI: Executa a importação pesada noutra thread (núcleo) do processador!
                // Isto impede que a aplicação congele enquanto o PowerPoint faz as imagens.
                var pptFolder = await Task.Run(() => _powerPointService.ImportAsImages(openFileDialog.FileName));

                if (pptFolder != null)
                {
                    // Adiciona a nova pasta de slides à aba de Programação
                    // Assumindo que a tua lista se chama ScheduleItems
                    ScheduleItems.Add(pptFolder);
                    SaveCurrentSession();
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("Importar PowerPoint", ex);
                MessageBox.Show($"Erro ao importar PowerPoint: {ex.Message}", "Importar PowerPoint", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 3. Restaura o cursor normal quer a importação corra bem ou dê erro
                Mouse.OverrideCursor = null;
            }
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

    // NOVA FUNÇÃO para encontrar a ListBox, independentemente de onde os botões estejam
    private ListBox? GetInnerListBox(DependencyObject child)
    {
        DependencyObject current = child;
        Expander? expander = null;

        // 1. Sobe na árvore até encontrar o Expander desta pasta específica
        while (current != null)
        {
            if (current is Expander exp)
            {
                expander = exp;
                break;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        // 2. Se encontrou o Expander, desce na árvore para encontrar a ListBox com as músicas
        if (expander != null)
        {
            return FindVisualChild<ListBox>(expander);
        }

        return null;
    }

    // Função auxiliar que pesquisa elementos visuais "para baixo"
    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child != null && child is T t)
            {
                return t;
            }
            else
            {
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
        }
        return null;
    }

    private void BtnMoveSubItemUp_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is ScheduleItem folder)
    {
        var listBox = GetInnerListBox(btn);
        
        // Verifica se há alguma música selecionada nesta pasta
        if (listBox?.SelectedItem is ScheduleItem selectedSubItem)
        {
            int index = folder.SubItems.IndexOf(selectedSubItem);
            
            // Só sobe se não for já o primeiro item (0)
            if (index > 0)
            {
                folder.SubItems.RemoveAt(index);
                folder.SubItems.Insert(index - 1, selectedSubItem);
                
                // Mantém a música selecionada para permitir clicar várias vezes rapidamente
                listBox.SelectedItem = selectedSubItem;
                SaveCurrentSession();
            }
        }
    }
}

private void BtnMoveSubItemDown_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is ScheduleItem folder)
    {
        var listBox = GetInnerListBox(btn);
        
        if (listBox?.SelectedItem is ScheduleItem selectedSubItem)
        {
            int index = folder.SubItems.IndexOf(selectedSubItem);
            
            // Só desce se não for já o último item
            if (index >= 0 && index < folder.SubItems.Count - 1)
            {
                folder.SubItems.RemoveAt(index);
                folder.SubItems.Insert(index + 1, selectedSubItem);
                
                listBox.SelectedItem = selectedSubItem;
                SaveCurrentSession();
            }
        }
    }
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
            else
            {
                OpenProjectionWindow();
                _projectionWindow?.ShowAlert(dlg.MessageText, dlg.ShowClock, dlg.CountdownMinutes);
            }
            SyncAlertPreview();
        }
    }

    private async void LoadBackgroundGallery()
    {
        try
        {
            // 1. Vai à pasta e gera as miniaturas numa thread paralela (não bloqueia a UI!)
            var bgs = await Task.Run(() =>
            {
                var list = _bgGalleryService.GetBackgrounds();
                // Decodifica as miniaturas (150px, congeladas) já aqui, para a lista não "engasgar" ao aparecer
                foreach (var b in list) _ = b.OptimizedThumbnail;
                return list;
            });

            // 2. Volta à Thread Principal para atualizar os ecrãs em segurança
            Dispatcher.Invoke(() =>
            {
                AvailableBackgrounds.Clear();

                foreach (var bg in bgs)
                {
                    AvailableBackgrounds.Add(bg);
                }

                ApplyBackgroundFilter();
            });
        }
        catch (Exception ex)
        {
            // Impede que a aplicação crashe caso haja um vídeo corrompido
            System.Windows.MessageBox.Show($"Erro ao carregar galeria: {ex.Message}");
        }
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

    // ---------------------------------------------------------
    // DRAG & DROP DAS MÚSICAS PARA A PROGRAMAÇÃO
    // ---------------------------------------------------------
    private void LstSongs_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && LstSongs.SelectedItem is Song selectedSong && ExceededSongDragThreshold(e))
        {
            var item = new ScheduleItem
            {
                Title = selectedSong.Title,
                Subtitle = string.IsNullOrWhiteSpace(selectedSong.Artist) ? "Música" : selectedSong.Artist,
                ItemTypeIcon = "🎵",
                Slides = selectedSong.ParseSlides(_projectionSettings.MaxLinesPerSlide)
            };
            StartDrag(LstSongs, item);
        }
    }

    private bool ExceededSongDragThreshold(System.Windows.Input.MouseEventArgs e)
    {
        Vector diff = _songDragStartPoint - e.GetPosition(null);
        return Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
               Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
    }

    private void ApplyBackgroundFilter()
    {
        // Se vier de uma thread paralela, força a execução na thread principal da UI
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(ApplyBackgroundFilter);
            return;
        }

        var list = AvailableBackgrounds.AsEnumerable();

        if (RbFilterVideos?.IsChecked == true) list = list.Where(b => b.IsVideo);
        else if (RbFilterImages?.IsChecked == true) list = list.Where(b => !b.IsVideo);

        string query = TxtSearchBackground?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(query))
        {
            if (CmbSearchBgMode != null && CmbSearchBgMode.SelectedIndex == 1)
                list = list.Where(b => b.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            else
                list = list.Where(b => b.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredBackgrounds = new ObservableCollection<MediaBackground>(list);
        if (LstBackgrounds != null) LstBackgrounds.ItemsSource = FilteredBackgrounds;
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
    if (e.LeftButton == MouseButtonState.Pressed && _isSchedulePotentialDrag)
    {
        Point mousePos = e.GetPosition(null);
        Vector diff = _scheduleDragStartPoint - mousePos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            _isSchedulePotentialDrag = false;

            // EM VEZ DE USAR 'LstSchedule.SelectedItem', VAMOS BUSCAR EXATAMENTE O ELEMENTO DEBAIXO DO RATO
            if (e.OriginalSource is FrameworkElement frameworkElement && 
                frameworkElement.DataContext is ScheduleItem itemArrastado && 
                sender is ListBox listBoxOrigem)
            {
                DragDrop.DoDragDrop(listBoxOrigem, itemArrastado, DragDropEffects.Move);
            }
        }
    }
}

private void LstSchedule_Drop(object sender, System.Windows.DragEventArgs e)
{
    if (e.Data.GetData(typeof(ScheduleItem)) is ScheduleItem itemArrastado)
    {
        int dropIndex = -1;
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is ScheduleItem targetItem)
        {
            dropIndex = ScheduleItems.IndexOf(targetItem);
        }

        // 1. REMOVE DA ORIGEM (Da raiz ou de dentro de uma pasta)
        if (ScheduleItems.Contains(itemArrastado))
        {
            ScheduleItems.Remove(itemArrastado);
        }
        else
        {
            var parentFolder = ScheduleItems.FirstOrDefault(f => f.IsFolder && f.SubItems.Contains(itemArrastado));
            if (parentFolder != null) parentFolder.SubItems.Remove(itemArrastado);
        }

        // 2. INSERE NO NOVO DESTINO (Na raiz do Alinhamento)
        if (dropIndex != -1 && dropIndex <= ScheduleItems.Count)
        {
            ScheduleItems.Insert(dropIndex, itemArrastado);
        }
        else
        {
            ScheduleItems.Add(itemArrastado);
        }

        LstSchedule.SelectedItem = itemArrastado;
        SaveCurrentSession();
    }
}

    private void LstSchedule_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isSchedulePotentialDrag = false;
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
        if (sender is FrameworkElement el && el.DataContext is MediaBackground bg)
        {
            _pendingHoverPath = bg.FilePath;

            // Só carrega o vídeo/imagem se o rato parar em cima da miniatura durante 120ms!
            if (_hoverPreviewTimer == null)
            {
                _hoverPreviewTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(120)
                };
                _hoverPreviewTimer.Tick += (s, args) =>
                {
                    _hoverPreviewTimer.Stop();
                    SetPreviewMediaBackground(_pendingHoverPath);
                    ShowHoverBadge();
                };
            }

            _hoverPreviewTimer.Stop();
            _hoverPreviewTimer.Start();
        }
    }

    private void BackgroundItem_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverPreviewTimer?.Stop();
        RefreshMonitor();
        ApplyBadge(_currentMode);
    }

    private void LstBackgrounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstBackgrounds.SelectedItem is MediaBackground bg)
        {
            _currentBackgroundPath = bg.FilePath;
            PushBackgroundToProjector();
            RefreshMonitor();
        }
    }

    private void BtnAddSeparator_Click(object sender, RoutedEventArgs e)
    {
        var newSeparator = new ScheduleItem
        {
            Title = "NOVO SEPARADOR",
            ItemTypeIcon = "🏷️",
            IsSeparator = true
        };

        // Assume que a tua lista se chama ScheduleItems
        ScheduleItems.Add(newSeparator);

        // Seleciona o novo item e força a abertura da janela de renomear
        LstSchedule.SelectedItem = newSeparator;
        LstSchedule.ScrollIntoView(newSeparator);

        TxtRenameItem.Text = newSeparator.Title;
        OverlayRename.Visibility = Visibility.Visible;
        TxtRenameItem.Focus();
        TxtRenameItem.SelectAll();
    }

    private void BtnClearBackground_Click(object sender, RoutedEventArgs e)
    {
        _currentBackgroundPath = null;
        LstBackgrounds.SelectedItem = null;
        PushBackgroundToProjector();
        RefreshMonitor();
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
            // Só reposiciona (ex.: mudança de ecrã) se a janela de projeção já estiver aberta;
            // guardar definições não deve abrir a projeção.
            if (_projectionWindow != null) OpenProjectionWindow();
        }
    }

    private void MenuRemoveAnyItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ScheduleItem itemToDelete)
        {
            // 1. Tenta remover da lista principal
            if (ScheduleItems.Contains(itemToDelete))
            {
                ScheduleItems.Remove(itemToDelete);
            }
            else
            {
                // 2. Se não estiver na raiz, procura dentro das pastas e remove
                foreach (var folder in ScheduleItems.Where(f => f.IsFolder))
                {
                    if (folder.SubItems.Contains(itemToDelete))
                    {
                        folder.SubItems.Remove(itemToDelete);
                        break;
                    }
                }
            }
            SaveCurrentSession();
        }
    }

    // Agrupa várias alterações seguidas numa só gravação (400 ms depois da última)
    private void SaveCurrentSession()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer!.Stop();
                SaveSessionNow();
            };
        }
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveSessionNow()
    {
        try
        {
            // Grava num ficheiro temporário e só depois substitui, para nunca deixar o autosave a meio
            string tmp = AutoSavePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(ScheduleItems, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, AutoSavePath, true);
        }
        catch (Exception ex) { AppPaths.Log("Gravar autosave", ex); }
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
        catch (Exception ex)
        {
            AppPaths.Log("Restaurar sessão", ex);
            // Guarda uma cópia do ficheiro estragado para não ser sobrescrito
            try { File.Copy(AutoSavePath, AutoSavePath + ".corrompido", true); } catch { }
        }
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

            // Vê se a bolinha do "Modo 2 (Palavras)" está selecionada
            bool searchByWords = CmbSearchBibleMode != null && CmbSearchBibleMode.SelectedIndex == 1;

            var results = _bibleService.Search(TxtSearchBible.Text, version, searchByWords);
            LstBibleResults.ItemsSource = results;

            if (results.Count > 0)
            {
                if (TxtSearchBible.Text.Contains("-")) LstBibleResults.SelectAll();
                else LstBibleResults.SelectedIndex = 0;

                Keyboard.ClearFocus();
                LstBibleResults.Focus();
            }
        }
    }

    private void TxtSearchBackground_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyBackgroundFilter();
    }

    private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
    {
        var novaPasta = new ScheduleItem
        {
            Title = "NOVA PASTA",
            ItemTypeIcon = "📁", // Ou o ícone que preferires
            IsFolder = true,
            IsExpanded = true
        };

        ScheduleItems.Add(novaPasta);
        LstSchedule.SelectedItem = novaPasta;
        LstSchedule.ScrollIntoView(novaPasta);
    }

    private void MenuEditSong_Click(object sender, RoutedEventArgs e)
    {
        // Apanha a música exata onde o botão direito foi clicado
        if (sender is MenuItem menuItem && menuItem.DataContext is Song songToEdit)
        {
            var editor = new SongEditorWindow(songToEdit) { Owner = this };
            if (editor.ShowDialog() == true && editor.ResultSong != null)
            {
                _dbService.UpdateSong(editor.ResultSong);
                _allSongs = _dbService.GetAllSongs();
                UpdateSongFilter(TxtSearchSong.Text);
            }
        }
    }

    private void MenuDeleteSong_Click(object sender, RoutedEventArgs e)
    {
        // Apanha a música exata onde o botão direito foi clicado
        if (sender is MenuItem menuItem && menuItem.DataContext is Song songToDelete)
        {
            var result = MessageBox.Show(
                $"Tem a certeza que deseja apagar a música '{songToDelete.Title}'?\nEsta ação não pode ser revertida.",
                "Apagar Música",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _dbService.DeleteSong(songToDelete.Id);
                _allSongs = _dbService.GetAllSongs();
                UpdateSongFilter(TxtSearchSong.Text);

                // Limpa o painel central se a música apagada estiver a ser projetada
                if (TxtSelectedSongTitle.Text == songToDelete.Title)
                {
                    TxtSelectedSongTitle.Text = "Nenhum item selecionado";
                    LstSlides.ItemsSource = null;
                }
            }
        }
    }

    private void InnerList_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (sender is ListBox innerListBox && innerListBox.DataContext is ScheduleItem pastaDestino && pastaDestino.IsFolder)
        {
            if (e.Data.GetDataPresent(typeof(ScheduleItem)))
            {
                var itemArrastado = e.Data.GetData(typeof(ScheduleItem)) as ScheduleItem;
                if (itemArrastado == null || itemArrastado == pastaDestino) return;

                // 1. REMOVER DA ORIGEM UNIVERSAL (Raiz ou qualquer outra pasta)
                if (ScheduleItems.Contains(itemArrastado))
                {
                    ScheduleItems.Remove(itemArrastado);
                }
                else
                {
                    // Procura a pasta exata onde o item estava e remove-o de lá
                    var pastaOrigem = ScheduleItems.FirstOrDefault(f => f.IsFolder && f.SubItems.Contains(itemArrastado));
                    if (pastaOrigem != null)
                    {
                        pastaOrigem.SubItems.Remove(itemArrastado);
                    }
                }

                // 2. DESCOBRIR A POSIÇÃO DE DESTINO
                int dropIndex = pastaDestino.SubItems.Count;

                var elementUnderMouse = e.OriginalSource as FrameworkElement;
                if (elementUnderMouse?.DataContext is ScheduleItem itemAlvo && itemAlvo != pastaDestino)
                {
                    int indexAlvo = pastaDestino.SubItems.IndexOf(itemAlvo);
                    if (indexAlvo >= 0)
                    {
                        dropIndex = indexAlvo;
                    }
                }

                // 3. INSERIR NA NOVA PASTA
                pastaDestino.SubItems.Insert(dropIndex, itemArrastado);

                pastaDestino.IsExpanded = true;
                innerListBox.SelectedItem = itemArrastado;

                SaveCurrentSession();
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


    private void InnerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true; // Impede que o clique confunda a lista principal

        if (sender is ListBox innerList && innerList.SelectedItem is ScheduleItem itemSelecionado)
        {
            // 1. Limpa a seleção da lista principal (para não teres dois itens azuis ao mesmo tempo)
            if (LstSchedule.SelectedItem != null)
            {
                LstSchedule.SelectedIndex = -1;
            }

            // 2. Carrega os slides do item (igual ao que LstSchedule_SelectionChanged faz)
            TxtSelectedSongTitle.Text = itemSelecionado.Title;
            LstSlides.ItemsSource = itemSelecionado.Slides;
            if (itemSelecionado.Slides.Count > 0) LstSlides.SelectedIndex = 0;

            // 3. Atualiza o estado Ativo (Borda Verde/Dourada)
            foreach (var folder in ScheduleItems)
            {
                folder.IsActive = false;
                foreach (var sub in folder.SubItems) sub.IsActive = false;
            }
            itemSelecionado.IsActive = true;
        }
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
            SendToProjection(slide.Content, slide.Reference);
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
            foreach (var sch in ScheduleItems)
            {
                sch.IsActive = (sch == item);
                foreach (var sub in sch.SubItems) sub.IsActive = false;
            }
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
            try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(ScheduleItems, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex)
            {
                AppPaths.Log("Gravar culto", ex);
                MessageBox.Show($"Não foi possível gravar o culto: {ex.Message}", "Gravar Culto", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    if (ScheduleItems.Count > 0) LstSchedule.SelectedIndex = 0;
                    SaveCurrentSession();
                }
            }
            catch (Exception ex)
            {
                AppPaths.Log("Carregar culto", ex);
                MessageBox.Show($"Erro ao carregar o culto: {ex.Message}", "Abrir Culto", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

    // Substitui a função UpdateSongFilter inteira
    private void UpdateSongFilter(string query)
    {
        IEnumerable<Song> matches;

        if (string.IsNullOrWhiteSpace(query))
        {
            matches = _allSongs;
        }
        else
        {
            // Se CmbSearchSongMode existir e o Index for 1 ("Por Letra")
            if (CmbSearchSongMode != null && CmbSearchSongMode.SelectedIndex == 1)
            {
                matches = _allSongs.Where(s =>
                    (s.Title != null && s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (s.RawLyrics != null && s.RawLyrics.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                // Padrão (Index 0): Por Título
                matches = _allSongs.Where(s => s.Title != null && s.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase));
            }
        }

        FilteredSongs = new ObservableCollection<Song>(matches);
        if (LstSongs != null) LstSongs.ItemsSource = FilteredSongs;
    }

    private void SongSearchMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (TxtSearchSong != null) UpdateSongFilter(TxtSearchSong.Text);
    }

    private void TxtSearchSong_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Espera 200 ms depois da última letra, para não refiltrar a cada tecla
        if (_songSearchTimer == null)
        {
            _songSearchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _songSearchTimer.Tick += (s, args) =>
            {
                _songSearchTimer!.Stop();
                UpdateSongFilter(TxtSearchSong.Text);
            };
        }
        _songSearchTimer.Stop();
        _songSearchTimer.Start();
    }

    private void LstSongs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSongs.SelectedItem is Song selectedSong)
        {
            TxtSelectedSongTitle.Text = selectedSong.Title;
            LstSlides.ItemsSource = selectedSong.ParseSlides(_projectionSettings.MaxLinesPerSlide);
            if (LstSlides.Items.Count > 0) LstSlides.SelectedIndex = 0;
        }
    }

    private void LstSlides_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSlides.SelectedItem is SlideItem slide)
        {
            // 1. Fundo próprio do slide (ex.: PowerPoint) ou fundo geral; só muda o projetor se for diferente
            PushBackgroundToProjector();

            // 2. Texto do slide (o monitor é atualizado dentro de SendToProjection)
            SendToProjection(slide.Content, slide.Reference);
        }
    }

    private void SendToProjection(string text, string? reference = null)
    {
        _currentSlideText = text;
        _currentReference = reference ?? string.Empty;
        if (_projectionWindow != null && _currentMode == ProjectionMode.Live) _projectionWindow.UpdateText(text, reference);
        RefreshMonitor();
    }

    // ---------------------------------------------------------
    // MONITOR DE SAÍDA = espelho do que está realmente no projetor
    // ---------------------------------------------------------
    // Fundo que o projetor deve mostrar agora: logo (modo LOGO), fundo do slide, ou fundo geral
    private string? GetEffectiveBackground()
    {
        if (_currentMode == ProjectionMode.Logo) return _logoFilePath;
        if (LstSlides.SelectedItem is SlideItem slide && slide.HasCustomBackground) return slide.BackgroundPath;
        return _currentBackgroundPath;
    }

    // Só envia ao projetor se o fundo mudou (assim um vídeo em loop não reinicia a cada slide)
    private void PushBackgroundToProjector()
    {
        string? eff = GetEffectiveBackground();
        if (string.Equals(_projectorBgPath, eff, StringComparison.OrdinalIgnoreCase)) return;
        _projectorBgPath = eff;
        _projectionWindow?.SetMediaBackground(eff);
    }

    private void RefreshMonitor()
    {
        SetPreviewMediaBackground(GetEffectiveBackground());

        // Só há texto no ecrã em modo LIVE; em CLEAR e LOGO o monitor fica sem texto, tal como o projetor
        string raw = string.Empty;
        if (_currentMode == ProjectionMode.Live)
        {
            raw = _currentSlideText;
            if (!string.IsNullOrWhiteSpace(_currentReference)) raw += "\n\n— " + _currentReference;
        }
        _stageWindow?.SetText(raw);
        string text = _projectionSettings.Uppercase ? raw.ToUpperInvariant() : raw;
        if (TxtPreview.Text != text)
        {
            TxtPreview.Text = text;
            TxtPreview.BeginAnimation(UIElement.OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
        }
    }

    private void ApplyBadge(ProjectionMode mode)
    {
        var c = mode == ProjectionMode.Live ? Color.FromRgb(16, 185, 129) : Color.FromRgb(239, 68, 68);
        BadgeLiveDot.Fill = new SolidColorBrush(c);
        BadgeLiveText.Foreground = new SolidColorBrush(c);
        BadgeLiveBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, c.R, c.G, c.B));
        BadgeLiveText.Text = mode switch
        {
            ProjectionMode.Live => "EM DIRETO",
            ProjectionMode.Clear => "SÓ FUNDO (CLEAR)",
            _ => "LOGO ATIVO"
        };
    }

    // Ao passar o rato sobre um fundo da galeria o monitor mostra-o, mas avisa que não está no ecrã
    private void ShowHoverBadge()
    {
        var amber = Color.FromRgb(245, 158, 11);
        BadgeLiveDot.Fill = new SolidColorBrush(amber);
        BadgeLiveText.Foreground = new SolidColorBrush(amber);
        BadgeLiveBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(51, amber.R, amber.G, amber.B));
        BadgeLiveText.Text = "PRÉ-VISUALIZAÇÃO";
    }

    // Mostra no monitor o aviso/cronómetro que estiver ativo no projetor
    private void SyncAlertPreview()
    {
        bool active = _projectionWindow?.IsAlertActive == true;
        PreviewAlertOverlay.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (!active) return;

        TxtPreviewAlert.Text = _projectionWindow!.AlertMessageText;
        string clock = _projectionWindow.AlertClockText;
        TxtPreviewAlertClock.Text = clock;
        TxtPreviewAlertClock.Visibility = string.IsNullOrEmpty(clock) ? Visibility.Collapsed : Visibility.Visible;
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
        _isBlack = false;
        _currentMode = mode;

        BtnModeLogo.Style = (Style)FindResource(mode == ProjectionMode.Logo ? "GoldButton" : "NavyButton");
        BtnModeClear.Style = (Style)FindResource(mode == ProjectionMode.Clear ? "GoldButton" : "NavyButton");
        BtnModeLive.Style = (Style)FindResource(mode == ProjectionMode.Live ? "GoldButton" : "NavyButton");

        ApplyBadge(mode);
        _projectionWindow?.SetBlackout(false);
        PushBackgroundToProjector();

        if (mode == ProjectionMode.Live) _projectionWindow?.UpdateText(_currentSlideText, _currentReference);
        else _projectionWindow?.ClearScreen();

        RefreshMonitor();
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
            var newWindow = new ProjectionWindow();
            newWindow.PreviewKeyDown += ForwardPresenterKey;
            newWindow.Closed += (s, e) =>
            {
                if (ReferenceEquals(_projectionWindow, newWindow))
                {
                    _projectionWindow = null;
                    _stageWindow?.Close();
                }
                SyncAlertPreview();
            };
            _projectionWindow = newWindow;
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
            _projectorBgPath = GetEffectiveBackground();
            _projectionWindow.SetBlackout(false);
            _projectionWindow.SetMediaBackground(_projectorBgPath);
            if (_currentMode == ProjectionMode.Live) _projectionWindow.UpdateText(_currentSlideText, _currentReference);
            else _projectionWindow.ClearScreen();
        }

        EnsureStageWindow();
    }

    // ---------------------------------------------------------
    // ECRÃ DO GRUPO DE LOUVOR
    // ---------------------------------------------------------
    // Abre/move/fecha o ecrã do louvor conforme as definições (só com a projeção ativa e num ecrã diferente)
    private void EnsureStageWindow()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        int idx = _projectionSettings.StageScreenIndex;
        bool wanted = _projectionWindow != null && screens.Length > 1
                      && idx >= 0 && idx < screens.Length && idx != _projectionSettings.ScreenIndex;

        if (!wanted)
        {
            _stageWindow?.Close();
            _stageWindow = null;
            return;
        }

        var bounds = screens[idx].Bounds;

        if (_stageWindow != null && (Math.Abs(_stageWindow.Left - bounds.Left) > 50 || Math.Abs(_stageWindow.Top - bounds.Top) > 50))
        {
            _stageWindow.Close();
            _stageWindow = null;
        }

        if (_stageWindow == null)
        {
            var stage = new StageWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height
            };
            stage.PreviewKeyDown += ForwardPresenterKey;
            stage.Closed += (s, e) => { if (ReferenceEquals(_stageWindow, stage)) _stageWindow = null; };
            _stageWindow = stage;
            stage.Show();
            stage.WindowState = WindowState.Maximized;
        }

        _stageWindow.ApplySettings(_projectionSettings);
        RefreshMonitor(); // envia o texto atual ao ecrã do louvor
    }

    // Teclas da caneta (Logitech R400 e semelhantes): Page Up/Down, setas, "." ou B (ecrã preto), F5/Esc
    private bool _isBlack;
    private ProjectionMode _modeBeforeBlack = ProjectionMode.Live;

    private static bool IsPresenterPriorityKey(Key k) =>
        k == Key.PageDown || k == Key.PageUp || k == Key.Right || k == Key.Left || k == Key.B || k == Key.OemPeriod;

    private void ToggleBlack()
    {
        if (_isBlack)
        {
            SetProjectionMode(_modeBeforeBlack);
            return;
        }
        _modeBeforeBlack = _currentMode == ProjectionMode.Clear ? ProjectionMode.Live : _currentMode;
        SetProjectionMode(ProjectionMode.Clear);
        _projectionWindow?.SetBlackout(true);
        _isBlack = true;
    }

    private bool HandlePresenterKey(Key key)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return false;
        switch (key)
        {
            case Key.Escape: SetProjectionMode(ProjectionMode.Clear); return true;
            case Key.F6: SetProjectionMode(ProjectionMode.Logo); return true;
            case Key.F5: SetProjectionMode(ProjectionMode.Live); return true;
            case Key.B:
            case Key.OemPeriod: ToggleBlack(); return true;
            case Key.Down: case Key.Space: case Key.PageDown: case Key.Right: NextSlide(); return true;
            case Key.Up: case Key.PageUp: case Key.Left: PrevSlide(); return true;
        }
        return false;
    }

    // Antes de a lista/tabs "engolirem" a tecla
    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;
        if (IsPresenterPriorityKey(e.Key) && HandlePresenterKey(e.Key)) e.Handled = true;
    }

    private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;
        if (HandlePresenterKey(e.Key)) e.Handled = true;
    }

    // Janelas de projeção/louvor reencaminham as teclas para a janela principal
    private void ForwardPresenterKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (HandlePresenterKey(e.Key)) e.Handled = true;
    }

    private void BtnNextSlide_Click(object sender, RoutedEventArgs e) => NextSlide();

    private void BtnPrevSlide_Click(object sender, RoutedEventArgs e) => PrevSlide();

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
        // OTIMIZAÇÃO: Se este fundo já está no Preview, não faz nada!
        if (string.Equals(_lastLoadedPreviewPath, filePath, StringComparison.OrdinalIgnoreCase))
            return;

        _lastLoadedPreviewPath = filePath;

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            PreviewBackgroundImage.Source = null;
            return;
        }

        // Tenta encontrar o ficheiro na nossa galeria já carregada em RAM
        var knownBg = AvailableBackgrounds.FirstOrDefault(b => string.Equals(b.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (knownBg != null && knownBg.OptimizedThumbnail != null)
        {
            // Se encontrou (seja vídeo ou foto), usa a miniatura levezinha que já está em memória! (0 custo de CPU/RAM extra)
            PreviewBackgroundImage.Source = knownBg.OptimizedThumbnail;
        }
        else
        {
            // Fallback: Se for um ficheiro de fora (ex: botão LOGO que não está na galeria)
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 320; // Carrega pequenino para poupar RAM
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                PreviewBackgroundImage.Source = bitmap;
            }
            catch { PreviewBackgroundImage.Source = null; }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e) { this.WindowState = WindowState.Minimized; }
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) { this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    private void BtnClose_Click(object sender, RoutedEventArgs e) { this.Close(); }
    protected override void OnClosed(EventArgs e) { _saveTimer?.Stop(); SaveSessionNow(); _remote?.Stop(); _stageWindow?.Close(); _projectionWindow?.Close(); base.OnClosed(e); }

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
            Background = new SolidColorBrush(Color.FromArgb(230, 21, 25, 31)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(34, 211, 238)),
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
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                var imported = EasyWorshipImporter.ImportFromEasyWorshipSqlite(dialog.FileName);

                // Ignora músicas que já existem (mesmo título e mesma letra), para não duplicar ao importar duas vezes
                static string Key(Song s) => (s.Title ?? "").Trim().ToLowerInvariant() + "|" + (s.RawLyrics ?? "").Trim();
                var existing = new HashSet<string>(_allSongs.Select(Key));
                var novas = imported.Where(s => existing.Add(Key(s))).ToList();

                if (novas.Count > 0)
                {
                    _dbService.InsertBatch(novas);
                    _allSongs = _dbService.GetAllSongs();
                    UpdateSongFilter(string.Empty);
                }
                Mouse.OverrideCursor = null;

                MessageBox.Show(
                    $"{novas.Count} músicas importadas." + (imported.Count - novas.Count > 0 ? $"\n{imported.Count - novas.Count} já existiam e foram ignoradas." : ""),
                    "Importar EasyWorship", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                AppPaths.Log("Importar EasyWorship", ex);
                MessageBox.Show($"Erro ao importar do EasyWorship:\n{ex.Message}", "Importar EasyWorship", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnOpenMedleyBuilder_Click(object sender, RoutedEventArgs e)
    {
        // _allSongs representa a lista atual de músicas carregadas da base de dados
        var medleyWindow = new MedleyBuilderWindow(_allSongs)
        {
            Owner = this
        };

        if (medleyWindow.ShowDialog() == true && medleyWindow.ResultingMedley != null)
        {
            // Se o utilizador clicou em "Adicionar à Programação"
            ScheduleItems.Add(medleyWindow.ResultingMedley);
            SaveCurrentSession();
        }
    }



}