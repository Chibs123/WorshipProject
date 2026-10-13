using System.Collections.Generic;
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

        // Esta propriedade diz ao XAML se deve pintar o cartão a verde
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public List<SlideItem> Slides { get; set; } = new();

        public override string ToString() => Title;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}