using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WorshipProjectionApp.Models;
using System.Windows.Threading;

namespace WorshipProjectionApp;

public partial class ProjectionWindow : Window
{
    private DispatcherTimer? _clockTimer;
    private TimeSpan _timeRemaining;
    private bool _isCountdown;
    private readonly Duration _fadeDuration = new(TimeSpan.FromMilliseconds(220));
    private bool _isBlackout;

    // Estado do aviso (lido pelo monitor da janela principal)
    public bool IsAlertActive { get; private set; }
    public string AlertMessageText => TxtProjectionAlert.Text;
    public string AlertClockText => TxtProjectionClock.Visibility == Visibility.Visible ? TxtProjectionClock.Text : string.Empty;

    private ProjectionSettings _settings = new();
    private string _pendingMain = string.Empty;
    private string _pendingRef = string.Empty;

    public ProjectionWindow()
    {
        InitializeComponent();
        // Os tamanhos dependem da altura/largura do ecrã: recalcula quando a janela muda de tamanho
        SizeChanged += (s, e) => ApplySettings(_settings);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    public void UpdateText(string newText, string? reference = null)
    {
        if (_isBlackout) return;

        if (string.IsNullOrWhiteSpace(newText))
        {
            ClearScreen();
            return;
        }

        string main = newText;
        string? refBottom = null;

        if (!string.IsNullOrWhiteSpace(reference))
        {
            if (_settings.BibleRefAtBottom) refBottom = reference;
            else main = newText + "\n\n— " + reference;
        }

        if (_settings.Uppercase) main = main.ToUpperInvariant();

        TransitionTo(main, refBottom);
    }

    public void ClearScreen() => TransitionTo(string.Empty, null);

    // Faz fade-out, troca o texto (e a referência) e faz fade-in. Usa sempre o último pedido,
    // por isso mudanças rápidas de slide não deixam texto antigo no ecrã.
    private void TransitionTo(string main, string? reference)
    {
        _pendingMain = main;
        _pendingRef = reference ?? string.Empty;

        if (main.Length > 0 && TxtProjection.Text == main && TxtReference.Text == _pendingRef && TextLayer.Opacity > 0.8) return;

        var fadeOut = new DoubleAnimation(0, _fadeDuration);
        fadeOut.Completed += (s, e) =>
        {
            SetTextNow(_pendingMain, _pendingRef);
            if (_pendingMain.Length > 0 && !_isBlackout)
                TextLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(1, _fadeDuration));
        };
        TextLayer.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SetTextNow(string main, string reference)
    {
        TxtProjection.Text = main;
        TxtReference.Text = reference;
        TextHost.Visibility = main.Length == 0 ? Visibility.Collapsed : Visibility.Visible; // a caixa só existe com texto
        TxtReference.Visibility = reference.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SetMediaBackground(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            FadeOutMedia();
            return;
        }

        string ext = System.IO.Path.GetExtension(filePath).ToLower();
        var videoExtensions = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };

        if (videoExtensions.Contains(ext))
        {
            FadeOutElement(MediaBgImage);

            try
            {
                MediaBgVideo.Source = new Uri(filePath, UriKind.Absolute);
                MediaBgVideo.Position = TimeSpan.Zero;
                MediaBgVideo.Play();
                FadeInElement(MediaBgVideo);
            }
            catch { }
        }
        else
        {
            FadeOutElement(MediaBgVideo, () =>
            {
                MediaBgVideo.Stop();
                MediaBgVideo.Source = null;
            });

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(filePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                MediaBgImage.Source = bmp;
                FadeInElement(MediaBgImage);
            }
            catch { }
        }
    }

    private void FadeInElement(UIElement element)
    {
        var anim = new DoubleAnimation(1, _fadeDuration);
        element.BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOutElement(UIElement element, Action? onComplete = null)
    {
        var anim = new DoubleAnimation(0, _fadeDuration);
        if (onComplete != null)
        {
            anim.Completed += (s, e) => onComplete();
        }
        element.BeginAnimation(OpacityProperty, anim);
    }

    private void FadeOutMedia()
    {
        FadeOutElement(MediaBgImage);
        FadeOutElement(MediaBgVideo, () =>
        {
            MediaBgVideo.Stop();
            MediaBgVideo.Source = null;
            MediaBgImage.Source = null;
        });
    }

    public void SetBlackout(bool blackout)
    {
        _isBlackout = blackout;

        if (_isBlackout)
        {
            FadeOutElement(TextLayer);
            FadeOutElement(MediaBgVideo);
            FadeOutElement(MediaBgImage);
        }
        else
        {
            FadeInElement(TextLayer);
            if (MediaBgVideo.Source != null) FadeInElement(MediaBgVideo);
            if (MediaBgImage.Source != null) FadeInElement(MediaBgImage);
        }
    }

    private void MediaBgVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        MediaBgVideo.Position = TimeSpan.Zero;
        MediaBgVideo.Play();
    }

    public void ShowAlert(string message, bool showClock = false, int countdownMinutes = 0)
    {
        IsAlertActive = true;
        TxtProjectionAlert.Text = message;
        _isCountdown = countdownMinutes > 0;

        if (_isCountdown)
        {
            _timeRemaining = TimeSpan.FromMinutes(countdownMinutes);
            TxtProjectionClock.Text = _timeRemaining.ToString(@"mm\:ss");
            TxtProjectionClock.Visibility = Visibility.Visible;
        }
        else if (showClock)
        {
            TxtProjectionClock.Text = DateTime.Now.ToString("HH:mm");
            TxtProjectionClock.Visibility = Visibility.Visible;
        }
        else
        {
            TxtProjectionClock.Text = string.Empty;
            TxtProjectionClock.Visibility = Visibility.Collapsed;
        }

        if (showClock || _isCountdown)
        {
            StartClockTimer();
        }
        else
        {
            _clockTimer?.Stop();
        }

        // Torna a Grid visível e faz o Fade In
        FullScreenAlertOverlay.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(1, _fadeDuration);
        FullScreenAlertOverlay.BeginAnimation(OpacityProperty, fadeIn);
    }

    public void HideAlert()
    {
        IsAlertActive = false;
        _clockTimer?.Stop();

        // Faz o Fade Out e, quando terminar, esconde a Grid completamente
        var fadeOut = new DoubleAnimation(0, _fadeDuration);
        fadeOut.Completed += (s, e) =>
        {
            TxtProjectionAlert.Text = string.Empty;
            TxtProjectionClock.Text = string.Empty;
            FullScreenAlertOverlay.Visibility = Visibility.Collapsed;
        };

        FullScreenAlertOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void StartClockTimer()
    {
        _clockTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick -= ClockTimer_Tick;
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
    }

    private void ClockTimer_Tick(object? sender, EventArgs e)
    {
        if (_isCountdown)
        {
            if (_timeRemaining.TotalSeconds > 0)
            {
                _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
                TxtProjectionClock.Text = _timeRemaining.ToString(@"mm\:ss");
            }
            else
            {
                TxtProjectionClock.Text = "00:00 (Fim)";
                _clockTimer?.Stop();
            }
        }
        else
        {
            TxtProjectionClock.Text = DateTime.Now.ToString("HH:mm");
        }
    }

    public void ApplySettings(ProjectionSettings settings)
    {
        _settings = settings;

        double w = ActualWidth > 0 ? ActualWidth : 1920;
        double h = ActualHeight > 0 ? ActualHeight : 1080;
        double scale = h / 1080.0;                 // todos os tamanhos são definidos para 1080p
        double fontSize = Math.Max(10, settings.TextFontSize * scale);

        var fontFamily = new System.Windows.Media.FontFamily(settings.FontFamily);
        var weight = settings.IsBold ? FontWeights.Bold : FontWeights.Normal;

        TxtProjection.FontFamily = fontFamily;
        TxtProjection.FontWeight = weight;
        TxtProjection.TextAlignment = settings.TextAlignment;
        TxtProjection.FontSize = fontSize;
        TxtProjection.LineHeight = fontSize * Math.Max(0.8, settings.LineSpacing);
        TxtProjection.StrokeThickness = settings.OutlineEnabled ? Math.Max(0, settings.OutlineThickness * scale) : 0;

        var converter = new BrushConverter();
        System.Windows.Media.Brush? textBrush = null;
        try { textBrush = converter.ConvertFromString(settings.FontColorHex) as System.Windows.Media.Brush; } catch { }
        textBrush ??= System.Windows.Media.Brushes.White;
        TxtProjection.Foreground = textBrush;

        if (TxtProjection.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
        {
            shadow.BlurRadius = settings.ShadowBlurRadius * scale;
            shadow.ShadowDepth = 4 * scale;
            shadow.Opacity = settings.ShadowOpacity;
        }

        // Caixa semitransparente atrás do texto
        if (settings.BackBoxEnabled)
        {
            byte alpha = (byte)Math.Round(255 * Math.Clamp(settings.BackBoxOpacity, 0, 1));
            TextHost.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
            TextHost.Padding = new Thickness(fontSize * 0.5, fontSize * 0.25, fontSize * 0.5, fontSize * 0.25);
            TextHost.CornerRadius = new CornerRadius(fontSize * 0.25);
        }
        else
        {
            TextHost.Background = null;
            TextHost.Padding = new Thickness(0);
            TextHost.CornerRadius = new CornerRadius(0);
        }

        OverlayDimmer.Opacity = Math.Clamp(settings.DimmerOpacity, 0, 0.9);

        // Margens de segurança e largura máxima do texto (o texto quebra aqui; se ficar alto demais, reduz)
        double marginPixels = w * (settings.MarginPercent / 100.0);
        double bottomExtra = (settings.BibleRefAtBottom && settings.VerticalAlignment == VerticalAlignment.Bottom) ? h * 0.07 : 0; // espaço para a referência
        ViewboxContent.VerticalAlignment = settings.VerticalAlignment;
        ViewboxContent.Margin = new Thickness(marginPixels, marginPixels, marginPixels, marginPixels + bottomExtra);
        double boxPad = settings.BackBoxEnabled ? fontSize : 0;
        TxtProjection.MaxWidth = Math.Max(200, w - 2 * marginPixels - boxPad);

        // Referência bíblica
        TxtReference.FontFamily = fontFamily;
        TxtReference.FontSize = Math.Max(12, settings.TextFontSize * 0.42 * scale);
        TxtReference.Foreground = textBrush;
        TxtReference.MaxWidth = Math.Max(200, w - 2 * marginPixels);
        TxtReference.Margin = new Thickness(0, 0, 0, h * 0.04);
    }
}
