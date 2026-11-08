using System.Windows;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;
using Application = System.Windows.Application;

namespace WorshipProjectionApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // O tema tem de estar aplicado ANTES de a janela principal ser criada
            var settings = ProjectionSettings.Load();
            ThemeService.Apply(ThemeService.Get(settings.ThemeId));

            base.OnStartup(e);
        }
    }
}
