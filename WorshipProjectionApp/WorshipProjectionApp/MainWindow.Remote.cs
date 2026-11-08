using System.Linq;
using System.Windows;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;

namespace WorshipProjectionApp;

// Parte da janela principal que atende o comando remoto (telemóvel). Tudo o que toca na UI passa pelo Dispatcher.
public partial class MainWindow
{
    private RemoteControlService? _remote;

    private void StartRemote()
    {
        try
        {
            _remote = new RemoteControlService(this);
            _remote.Start();
        }
        catch (Exception ex) { AppPaths.Log("Remote.StartRemote", ex); }
    }

    internal object RemoteGetState() => Dispatcher.Invoke<object>(() =>
    {
        var slides = new List<object>();
        int idx = 0;
        foreach (var o in LstSlides.Items)
        {
            if (o is SlideItem s) slides.Add(new { i = idx, header = s.Header, text = s.Content, reference = s.Reference });
            idx++;
        }

        var schedule = new List<object>();
        for (int i = 0; i < ScheduleItems.Count; i++)
        {
            var it = ScheduleItems[i];
            var subs = new List<object>();
            for (int j = 0; j < it.SubItems.Count; j++)
            {
                var sub = it.SubItems[j];
                subs.Add(new { id = $"{i}.{j}", title = sub.Title, subtitle = sub.Subtitle, icon = sub.ItemTypeIcon, active = sub.IsActive });
            }
            schedule.Add(new { id = i.ToString(), title = it.Title, subtitle = it.Subtitle, icon = it.ItemTypeIcon, active = it.IsActive, folder = it.IsFolder, separator = it.IsSeparator, subs });
        }

        return new
        {
            mode = _currentMode.ToString().ToLowerInvariant(),
            black = _isBlack,
            title = TxtSelectedSongTitle.Text,
            text = _currentSlideText,
            reference = _currentReference,
            slideIndex = LstSlides.SelectedIndex,
            slides,
            schedule
        };
    });

    internal bool RemoteCommand(string name) => Dispatcher.Invoke(() =>
    {
        switch (name)
        {
            case "next": NextSlide(); return true;
            case "prev": PrevSlide(); return true;
            case "black": ToggleBlack(); return true;
            case "clear": SetProjectionMode(ProjectionMode.Clear); return true;
            case "logo": SetProjectionMode(ProjectionMode.Logo); return true;
            case "live": SetProjectionMode(ProjectionMode.Live); return true;
        }
        return false;
    });

    internal bool RemoteSelectItem(string id) => Dispatcher.Invoke(() =>
    {
        var parts = id.Split('.');
        if (!int.TryParse(parts[0], out int i) || i < 0 || i >= ScheduleItems.Count) return false;

        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out int j) || j < 0 || j >= ScheduleItems[i].SubItems.Count) return false;
            var sub = ScheduleItems[i].SubItems[j];
            LstSchedule.SelectedIndex = -1;
            TxtSelectedSongTitle.Text = sub.Title;
            LstSlides.ItemsSource = sub.Slides;
            if (sub.Slides.Count > 0) LstSlides.SelectedIndex = 0;
            foreach (var folder in ScheduleItems)
            {
                folder.IsActive = false;
                foreach (var s in folder.SubItems) s.IsActive = false;
            }
            sub.IsActive = true;
            return true;
        }

        var item = ScheduleItems[i];
        if (item.IsFolder || item.IsSeparator) return false;
        LstSchedule.SelectedIndex = i;
        return true;
    });

    internal bool RemoteSelectSlide(int n) => Dispatcher.Invoke(() =>
    {
        if (n < 0 || n >= LstSlides.Items.Count) return false;
        LstSlides.SelectedIndex = n;
        return true;
    });

    internal object RemoteSearchSongs(string q, bool inLyrics) => Dispatcher.Invoke<object>(() =>
    {
        q = q.Trim();
        if (q.Length == 0) return new List<object>();
        return _allSongs
            .Where(s => (s.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                     || (inLyrics && (s.RawLyrics ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Take(40)
            .Select(s => (object)new { id = s.Id, title = s.Title, artist = s.Artist })
            .ToList();
    });

    internal bool RemoteAddSong(int id, bool project) => Dispatcher.Invoke(() =>
    {
        var song = _allSongs.FirstOrDefault(s => s.Id == id);
        if (song == null) return false;
        var item = new ScheduleItem
        {
            Title = song.Title,
            Subtitle = string.IsNullOrWhiteSpace(song.Artist) ? "Música" : song.Artist,
            ItemTypeIcon = "🎵",
            Slides = song.ParseSlides(_projectionSettings.MaxLinesPerSlide)
        };
        AddRemoteItem(item, project);
        return true;
    });

    internal List<string> RemoteBibleVersions() => _bibleService.GetAvailableVersions();

    internal object RemoteSearchBible(string q, string version)
    {
        if (string.IsNullOrWhiteSpace(version)) version = _bibleService.GetAvailableVersions().FirstOrDefault() ?? "";
        return _bibleService.Search(q, version)
            .Where(v => v.BookName != "Aviso" && v.BookName != "Erro")
            .Select(v => (object)new { @ref = v.Reference, text = v.Text.Trim() })
            .ToList();
    }

    internal bool RemoteAddBible(string q, string version, int from, int to, bool project)
    {
        if (string.IsNullOrWhiteSpace(version)) version = _bibleService.GetAvailableVersions().FirstOrDefault() ?? "";
        var verses = _bibleService.Search(q, version).Where(v => v.BookName != "Aviso" && v.BookName != "Erro").ToList();
        if (verses.Count == 0) return false;
        from = Math.Clamp(from, 0, verses.Count - 1);
        to = Math.Clamp(to, from, verses.Count - 1);
        var sel = verses.Skip(from).Take(to - from + 1).ToList();

        ScheduleItem item;
        if (sel.Count == 1)
        {
            item = new ScheduleItem
            {
                Title = sel[0].Reference,
                Subtitle = "Passagem Bíblica",
                ItemTypeIcon = "📖",
                Slides = new List<SlideItem> { sel[0].ToSlideItem() }
            };
        }
        else
        {
            item = new ScheduleItem
            {
                Title = $"{sel.First().Reference} a {sel.Last().Reference}",
                Subtitle = $"Leitura Bíblica ({sel.Count} versículos)",
                ItemTypeIcon = "📖",
                Slides = sel.Select(v => v.ToSlideItem()).ToList()
            };
        }
        Dispatcher.Invoke(() => AddRemoteItem(item, project));
        return true;
    }

    // Já corre na thread da UI
    private void AddRemoteItem(ScheduleItem item, bool project)
    {
        ScheduleItems.Add(item);
        SaveCurrentSession();
        if (project)
        {
            LstSchedule.SelectedIndex = ScheduleItems.Count - 1;
            SetProjectionMode(ProjectionMode.Live);
        }
    }

    private void BtnRemoteInfo_Click(object sender, RoutedEventArgs e)
    {
        string text;
        if (_remote == null || !_remote.IsRunning)
        {
            text = "O servidor remoto não arrancou.\n\n" + (_remote?.LastError ?? "") + "\n\nVê o ficheiro errors.log em %AppData%\\WorshipProjectionApp.";
        }
        else
        {
            var urls = _remote.GetUrls();
            text = "No telemóvel (ligado ao MESMO Wi-Fi do PC), abre no browser um destes endereços:\n\n"
                 + (urls.Count > 0 ? string.Join("\n\n", urls) : "(não encontrei nenhuma rede ativa)")
                 + "\n\nPIN: " + _remote.Pin
                 + "\n\nDica: no browser do telemóvel, usa \"Adicionar ao ecrã inicial\" para ficar como uma app.";
        }

        var tb = new System.Windows.Controls.TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 15,
            Margin = new Thickness(16),
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = (TryFindResource("TextPrimary") as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.White,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto
        };
        var win = new Window
        {
            Title = "Controlo remoto (telemóvel)",
            Width = 560,
            Height = 380,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (TryFindResource("PanelBg") as System.Windows.Media.Brush) ?? System.Windows.Media.Brushes.Black,
            Content = tb
        };
        win.ShowDialog();
    }
}
