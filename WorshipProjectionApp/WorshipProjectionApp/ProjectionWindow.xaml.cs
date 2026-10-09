using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorshipProjectionApp;

public partial class ProjectionWindow : Window
{
    public ProjectionWindow()
    {
        InitializeComponent();
    }

    public void UpdateText(string text)
    {
        TxtProjection.Text = text;
    }

    public void SetMediaBackground(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            StopVideo();
            BackgroundImage.Source = null;
            return;
        }

        string ext = Path.GetExtension(filePath).ToLower();
        var videoExtensions = new[] { ".mp4", ".mov", ".wmv", ".avi", ".m4v" };

        if (videoExtensions.Contains(ext))
        {
            // Oculta imagem estática e arranca o vídeo
            BackgroundImage.Source = null;
            PlayVideo(filePath);
        }
        else
        {
            // Oculta vídeo e carrega imagem estática
            StopVideo();
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                BackgroundImage.Source = bitmap;
            }
            catch
            {
                BackgroundImage.Source = null;
            }
        }
    }

    private void PlayVideo(string videoPath)
    {
        try
        {
            MediaVideo.Source = new Uri(videoPath, UriKind.Absolute);
            MediaVideo.Position = TimeSpan.Zero;
            MediaVideo.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao abrir vídeo: {ex.Message}");
        }
    }

    private void StopVideo()
    {
        MediaVideo.Stop();
        MediaVideo.Source = null;
    }

    // Reinicia o vídeo no momento em que termina -> Loop contínuo perfeito
    private void MediaVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        MediaVideo.Position = TimeSpan.Zero;
        MediaVideo.Play();
    }

    public void ClearScreen()
    {
        TxtProjection.Text = string.Empty;
    }

    public void SetBlackout(bool isBlackout)
    {
        if (isBlackout)
        {
            StopVideo();
            BackgroundImage.Source = null;
            TxtProjection.Text = string.Empty;
        }
    }

    public void ApplySettings(Models.ProjectionSettings settings)
    {
        TxtProjection.FontFamily = new System.Windows.Media.FontFamily(settings.FontFamily);
        TxtProjection.FontWeight = settings.IsBold ? FontWeights.Bold : FontWeights.Normal;
        TxtProjection.TextAlignment = settings.TextAlignment;

        var converter = new System.Windows.Media.BrushConverter();
        if (converter.ConvertFromString(settings.FontColorHex) is SolidColorBrush brush)
        {
            TxtProjection.Foreground = brush;
        }

        if (TxtProjection.Effect is System.Windows.Media.Effects.DropShadowEffect shadow)
        {
            shadow.BlurRadius = settings.ShadowBlurRadius;
        }

        // Ajusta margens e alinhamento do Viewbox
        if (TxtProjection.Parent is Viewbox viewbox)
        {
            viewbox.VerticalAlignment = settings.VerticalAlignment;
            double marginValue = (this.ActualWidth > 0 ? this.ActualWidth : 1280) * (settings.MarginPercent / 100.0);
            viewbox.Margin = new Thickness(marginValue);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopVideo();
        base.OnClosed(e);
    }
}