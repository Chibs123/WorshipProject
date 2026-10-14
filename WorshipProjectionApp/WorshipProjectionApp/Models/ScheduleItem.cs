using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorshipProjectionApp.Models
{
    public class ScheduleItem : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private string _itemTypeIcon = "🎵";
        private bool _isActive;
        private bool _isExpanded = true; // Para as pastas começarem abertas por defeito

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(); }
        }

        public string ItemTypeIcon
        {
            get => _itemTypeIcon;
            set { _itemTypeIcon = value; OnPropertyChanged(); }
        }

        // Esta propriedade diz ao XAML se deve pintar o cartão com a cor principal (Ativo)
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        // Controla se a pasta está expandida/aberta no XAML
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        // Define se este item é uma pasta agregadora
        public bool IsFolder { get; set; } = false;

        // Define se é apenas um separador visual simples (opcional/legado)
        public bool IsSeparator { get; set; } = false;

        public List<SlideItem> Slides { get; set; } = new();

        // A lista mágica que guarda as músicas dentro desta pasta
        public ObservableCollection<ScheduleItem> SubItems { get; set; } = new();

        public override string ToString() => Title;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}