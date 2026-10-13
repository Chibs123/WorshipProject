using System.Windows;
using System.Windows.Media;
using WorshipProjectionApp.Models;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace WorshipProjectionApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Carrega as definições guardadas
            var settings = ProjectionSettings.Load();

            // Pinta a cor de destaque (AccentColor) na App Inteira em tempo real!
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(settings.AppThemeColorHex);
                Application.Current.Resources["AccentColor"] = new SolidColorBrush(color);
            }
            catch { } // Proteção caso a cor seja inválida
        }
    }
}