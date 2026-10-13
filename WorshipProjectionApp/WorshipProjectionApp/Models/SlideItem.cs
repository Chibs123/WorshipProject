using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorshipProjectionApp.Models
{
    public class SlideItem : INotifyPropertyChanged
    {
        public string Header { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;

        private string? _backgroundPath;
        public string? BackgroundPath
        {
            get => _backgroundPath;
            set
            {
                _backgroundPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCustomBackground));
            }
        }

        // Usado para mostrar o ícone "🖼️" no ecrã
        public bool HasCustomBackground => !string.IsNullOrEmpty(BackgroundPath);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}