using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;

namespace WorshipProjectionApp.Services;

/// <summary>Definição de um tema: 6 tons de fundo/contorno (do mais escuro ao mais claro no tema original), texto e acento.</summary>
public sealed record AppTheme(
    string Id, string Name, string Description, bool IsLight,
    string Canvas, string Panel, string Card, string Border, string Mid, string Muted,
    string Text, string Accent, string OnAccent);

/// <summary>
/// Aplica um tema em tempo real. Os XAML usam {DynamicResource N_xxxxxx} para os tons neutros;
/// cada tom é calculado a partir dos 6 tons-âncora do tema (interpolação pela luminosidade
/// que esse tom tinha no tema original), por isso um tema novo só precisa de ~9 cores.
/// </summary>
public static class ThemeService
{
    public static readonly IReadOnlyList<AppTheme> Themes = new List<AppTheme>
    {
        new("grafite-ciano", "Grafite e Ciano", "Neutros frios com um acento elétrico. Discreto para o operador.", false,
            "0B0D10", "15191F", "1C2128", "2C333D", "4B5563", "9AA4B2", "F8FAFC", "22D3EE", "0B0D10"),
        new("papel-indigo", "Papel e Índigo", "Tema claro com tinta azul-escura. Para salas muito iluminadas.", true,
            "E9EDF4", "FFFFFF", "F3F5FA", "D0D7E3", "8A94A8", "5A657C", "141A2B", "4338CA", "FFFFFF"),
        new("vitral", "Vitral", "Violeta profundo com coral. Mais calor e personalidade.", false,
            "0E0B16", "171221", "201A2D", "33294A", "5B4C7A", "A89CC0", "F6F2FC", "FF7A6B", "2A0B08"),
        new("floresta-salva", "Floresta e Salva", "Verde muito escuro com verde-salva. Descansa a vista em cultos longos.", false,
            "0A100D", "111A15", "18231D", "26372E", "4C6455", "93A89B", "F0F6F2", "A3E635", "14210A"),
        new("meia-noite", "Meia-Noite Dourada", "O tema original: azul-marinho com dourado.", false,
            "070A10", "111827", "1A2234", "26334D", "475569", "94A3B8", "F8FAFC", "D4AF37", "070A10"),
        new("oceano", "Oceano", "Azul-petróleo profundo com turquesa.", false,
            "06121A", "0C1E2A", "122A3A", "1E3D52", "3F6A85", "8FB3C9", "EAF6FC", "2DD4BF", "04201C"),
        new("rubi", "Rubi", "Tons de vinho escuro com rosa intenso.", false,
            "120A0D", "1C1115", "27171D", "43242D", "7A4A57", "C09AA5", "FBF1F3", "F43F5E", "FFFFFF"),
        new("brasa-ambar", "Brasa e Âmbar", "Preto quente com âmbar, como à luz de velas.", false,
            "100C08", "1A140D", "251C12", "3D2E1C", "7A5E38", "B8A184", "FAF3E8", "F59E0B", "1F1300"),
        new("nevoa-esmeralda", "Névoa e Esmeralda", "Tema claro verde-acinzentado, suave e limpo.", true,
            "E6EEEC", "FFFFFF", "F1F6F5", "CAD9D6", "7F9A95", "4D6561", "10201D", "059669", "FFFFFF"),
        new("tinta", "Tinta", "Preto neutro com branco. Máximo contraste, sem cor.", false,
            "0A0A0A", "141414", "1C1C1C", "2E2E2E", "5A5A5A", "A3A3A3", "FAFAFA", "F5F5F5", "0A0A0A"),
    };

    public static string CurrentId { get; private set; } = Themes[0].Id;

    // Tons neutros usados nos XAML (valores do tema original "Grafite e Ciano")
    private static readonly string[] Shades =
    {
        "0B0D10","080A0D","0E1115","090B0E","0F1216","12161B","14181E","15191F","1A1F26","1C2128","1F252D","222931",
        "252C35","2A323C","2B333D","2B3540","2C333D","3F4A58","566273","20262E","323A45","4B5563","6B7683","9AA4B2"
    };

    // Âncoras do tema original, da mais escura à mais clara
    private static readonly string[] OriginalAnchors = { "0B0D10", "15191F", "1C2128", "2C333D", "4B5563", "9AA4B2" };

    public static AppTheme Get(string? id) => Themes.FirstOrDefault(t => t.Id == id) ?? Themes[0];

    public static Color ParseColor(string hex) =>
        (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex);

    public static void Apply(AppTheme t)
    {
        var res = Application.Current.Resources;
        CurrentId = t.Id;

        var anchors = new[] { t.Canvas, t.Panel, t.Card, t.Border, t.Mid, t.Muted }.Select(ParseColor).ToArray();
        var stops = OriginalAnchors.Select(h => Luma(ParseColor(h))).ToArray();

        Color Shade(string hex) => Ramp(Luma(ParseColor(hex)), stops, anchors);

        foreach (var hex in Shades) Set(res, "N_" + hex, Shade(hex));

        var canvas = anchors[0];
        var panel = anchors[1];
        var accent = ParseColor(t.Accent);
        var white = Color.FromRgb(255, 255, 255);
        var black = Color.FromRgb(0, 0, 0);

        Set(res, "BgCanvas", canvas);
        Set(res, "PanelBg", panel);
        Set(res, "CardBg", anchors[2]);
        Set(res, "CardHoverBg", Shade("2A323C"));
        Set(res, "BorderDark", anchors[3]);
        Set(res, "ButtonNeutral", Shade("20262E"));
        Set(res, "ButtonNeutralHover", Shade("323A45"));
        Set(res, "TextPrimary", ParseColor(t.Text));
        Set(res, "TextMuted", anchors[5]);

        Set(res, "AccentColor", accent);
        Set(res, "AccentText", ParseColor(t.OnAccent));
        Set(res, "AccentSoft", t.IsLight ? Lerp(panel, accent, 0.14) : Lerp(canvas, accent, 0.28));
        Set(res, "AccentLight", t.IsLight ? Lerp(accent, black, 0.30) : Lerp(accent, white, 0.45));
        Set(res, "AccentDim", Lerp(canvas, accent, 0.60));
    }

    private static void Set(System.Windows.ResourceDictionary res, string key, Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        res[key] = b;
    }

    private static double Luma(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    private static Color Ramp(double luma, double[] stops, Color[] colors)
    {
        if (luma <= stops[0]) return colors[0];
        for (int i = 0; i < stops.Length - 1; i++)
        {
            if (luma <= stops[i + 1])
                return Lerp(colors[i], colors[i + 1], (luma - stops[i]) / (stops[i + 1] - stops[i]));
        }
        return colors[^1];
    }
}
