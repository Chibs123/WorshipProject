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

    public ProjectionWindow()
    {
        InitializeComponent();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    public void UpdateText(string newText)
    {
        if (_isBlackout) return;

        if (string.IsNullOrWhiteSpace(newText))
        {
            ClearScreen();
            return;
        }

        if (TxtProjection.Text == newText && TxtProjection.Opacity > 0.8) return;

        var fadeOut = new DoubleAnimation(0, _fadeDuration);
        fadeOut.Completed += (s, e) =>
        {
            TxtProjection.Text = newText;
            var fadeIn = new DoubleAnimation(1, _fadeDuration);
            TxtProjection.BeginAnimation(OpacityProperty, fadeIn);
        };

        TxtProjection.BeginAnimation(OpacityProperty, fadeOut);
    }

    public void ClearScreen()
    {
        var fadeOut = new DoubleAnimation(0, _fadeDuration);
        fadeOut.Completed += (s, e) => { TxtProjection.Text = string.Empty; };
        TxtProjection.BeginAnimation(OpacityProperty, fadeOut);
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
            FadeOutElement(TxtProjection);
            FadeOutElement(MediaBgVideo);
            FadeOutElement(MediaBgImage);
        }
        else
        {
            FadeInElement(TxtProjection);
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
        TxtProjection.FontFamily = new System.Windows.Media.FontFamily(settings.FontFamily);
        TxtProjection.FontWeight = settings.IsBold ? FontWeights.Bold : FontWeights.Normal;
        TxtProjection.TextAlignment = settings.TextAlignment;

        var converter = new BrushConverter();
        if (converter.ConvertFromString(settings.FontColorHex) is SolidColorBrush brush)
        {
            TxtProjection.Foreground = brush;
        }

        if (TxtProjection.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
        {
            shadow.BlurRadius = settings.ShadowBlurRadius;
            shadow.Opacity = settings.ShadowOpacity;
        }

        ViewboxContent.VerticalAlignment = settings.VerticalAlignment;

        double baseWidth = this.ActualWidth > 0 ? this.ActualWidth : 1920;
        double marginPixels = baseWidth * (settings.MarginPercent / 100.0);
        ViewboxContent.Margin = new Thickness(marginPixels);
    }
}