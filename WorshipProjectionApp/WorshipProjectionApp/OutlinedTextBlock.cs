using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace WorshipProjectionApp;

/// <summary>
/// Texto com contorno (o TextBlock do WPF não tem). Desenha a letra como geometria:
/// primeiro o contorno (por fora) e depois o preenchimento por cima.
/// Quebra linhas dentro do MaxWidth e respeita alinhamento e altura de linha.
/// </summary>
public class OutlinedTextBlock : FrameworkElement
{
    private const FrameworkPropertyMetadataOptions MeasureRender =
        FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(string.Empty, MeasureRender));
    public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(new FontFamily("Segoe UI"), MeasureRender));
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(48.0, MeasureRender));
    public static readonly DependencyProperty FontWeightProperty = DependencyProperty.Register(
        nameof(FontWeight), typeof(FontWeight), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(FontWeights.Normal, MeasureRender));
    public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
        nameof(TextAlignment), typeof(TextAlignment), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(TextAlignment.Center, MeasureRender));
    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight), typeof(double), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(0.0, MeasureRender));
    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(OutlinedTextBlock), new FrameworkPropertyMetadata(0.0, MeasureRender));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(OutlinedTextBlock),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public double LineHeight { get => (double)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    private Geometry? _geometry;

    private FormattedText Build(string text, double maxWidth, TextAlignment alignment)
    {
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal);
        var ft = new FormattedText(text, CultureInfo.CurrentUICulture, System.Windows.FlowDirection.LeftToRight, typeface,
            FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            TextAlignment = alignment
        };
        if (!double.IsInfinity(maxWidth)) ft.MaxTextWidth = Math.Max(1, maxWidth);
        if (LineHeight > 0) ft.LineHeight = LineHeight;
        return ft;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _geometry = null;
        string text = Text ?? string.Empty;
        if (text.Length == 0) return new Size(0, 0);

        double pad = Math.Max(0, StrokeThickness);
        double maxW = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width - 2 * pad;

        // 1.ª passagem (alinhado à esquerda) só para saber a largura da linha mais larga depois de quebrar
        var probe = Build(text, maxW, TextAlignment.Left);
        double width = Math.Ceiling(probe.Width) + 1;

        // 2.ª passagem na largura exata, com o alinhamento pedido
        var ft = Build(text, width, TextAlignment);
        _geometry = ft.BuildGeometry(new Point(0, 0));

        return new Size(width + 2 * pad, ft.Height + 2 * pad);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_geometry == null) return;
        double pad = Math.Max(0, StrokeThickness);

        dc.PushTransform(new TranslateTransform(pad, pad));
        if (pad > 0 && Stroke != null)
        {
            // Pen centrada no limite da letra com o dobro da espessura => contorno de "pad" para fora
            dc.DrawGeometry(null, new Pen(Stroke, pad * 2) { LineJoin = PenLineJoin.Round }, _geometry);
        }
        dc.DrawGeometry(Foreground, null, _geometry);
        dc.Pop();
    }
}
