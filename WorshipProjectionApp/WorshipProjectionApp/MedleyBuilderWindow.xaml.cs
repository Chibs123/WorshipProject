using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;
using System.Text.Json;
using MessageBox = System.Windows.MessageBox;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
namespace WorshipProjectionApp
{
    public partial class MedleyBuilderWindow : Window
    {
        public ObservableCollection<SlideItem> LeftSlides { get; set; } = new();
        public ObservableCollection<SlideItem> RightSlides { get; set; } = new();
        public ObservableCollection<SlideItem> MedleySlides { get; set; } = new();

        // Variável para devolver o Medley gerado à MainWindow
        public ScheduleItem? ResultingMedley { get; private set; }

        private readonly IEnumerable<Song> _allSongs; // Assumindo que tens uma classe Song

        public MedleyBuilderWindow(IEnumerable<Song> allSongs)
        {
            InitializeComponent();
            DataContext = this;
            _allSongs = allSongs.OrderBy(s => s.Title).ToList();

            // Carrega as músicas para as ComboBoxes
            CmbSongLeft.ItemsSource = _allSongs;
            CmbSongRight.ItemsSource = _allSongs;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // --- CARREGAR SLIDES QUANDO SE ESCOLHE UMA MÚSICA ---
        private void CmbSongLeft_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbSongLeft.SelectedItem is Song selectedSong)
                LoadSlidesToCollection(selectedSong, LeftSlides);
        }

        private void CmbSongRight_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbSongRight.SelectedItem is Song selectedSong)
                LoadSlidesToCollection(selectedSong, RightSlides);
        }


        private void LoadSlidesToCollection(Song song, ObservableCollection<SlideItem> targetCollection)
        {
            targetCollection.Clear();

            if (string.IsNullOrWhiteSpace(song.RawLyrics))
                return;

            try
            {
                // 1. A tua base de dados guarda os slides em formato JSON. 
                // Vamos converter esse texto estruturado de volta para uma lista de SlideItem real!
                var slides = JsonSerializer.Deserialize<List<SlideItem>>(song.RawLyrics);

                if (slides != null)
                {
                    foreach (var slide in slides)
                    {
                        targetCollection.Add(slide); // Agora sim, os cartões vão separar-se corretamente!
                    }
                }
            }
            catch
            {
                // 2. Plano B (Fallback): 
                // Se por acaso houver uma música importada do EasyWorship que não esteja em JSON mas sim em texto simples
                var blocks = song.RawLyrics.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var block in blocks)
                {
                    targetCollection.Add(new SlideItem { Header = "Slide", Content = block.Trim() });
                }
            }
        }

        // --- MOVER SLIDES PARA O CENTRO (CLONAGEM) ---
        private void LstLeftSlides_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstLeftSlides.SelectedItem is SlideItem item) AddToMedley(item);
        }

        private void LstRightSlides_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstRightSlides.SelectedItem is SlideItem item) AddToMedley(item);
        }

        private void AddToMedley(SlideItem originalItem)
        {
            // CRIAR UM NOVO OBJETO para evitar conflitos de referência de memória
            MedleySlides.Add(new SlideItem
            {
                Header = $"Medley Slide {MedleySlides.Count + 1}",
                Content = originalItem.Content,
                BackgroundPath = originalItem.BackgroundPath
            });
        }

        // --- GRAVAÇÃO: OPÇÃO A (Só para o Culto) ---
        private void BtnAddToSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (MedleySlides.Count == 0) return;

            string title = string.IsNullOrWhiteSpace(TxtMedleyTitle.Text) ? "Novo Medley" : TxtMedleyTitle.Text;

            ResultingMedley = new ScheduleItem
            {
                Title = title,
                Subtitle = "Medley",
                ItemTypeIcon = "🔗",
                IsFolder = false,
                Slides = MedleySlides.ToList()
            };

            DialogResult = true; // Fecha a janela e indica sucesso
        }

        // --- GRAVAÇÃO: OPÇÃO B (Base de Dados Definitiva) ---
        private void BtnSaveAsNewSong_Click(object sender, RoutedEventArgs e)
        {
            if (MedleySlides.Count == 0) return;

            string title = string.IsNullOrWhiteSpace(TxtMedleyTitle.Text) ? "Novo Medley" : TxtMedleyTitle.Text;

            // Juntar todos os textos com 2 quebras de linha para formar a letra completa
            string fullLyrics = string.Join("\n\n", MedleySlides.Select(s => s.Content));

            // Aqui chamas o teu serviço da Base de Dados para gravar a nova música
            // _databaseService.AddSong(new Song { Title = title, RawLyrics = fullLyrics });

            MessageBox.Show("Medley gravado na base de dados das Músicas com sucesso!", "Guardado", MessageBoxButton.OK, MessageBoxImage.Information);

            // Opcional: Adicionar logo à programação também
            BtnAddToSchedule_Click(sender, e);
        }

        private Point _startPoint;
        private SlideItem? _draggedItem;

        private void SlideList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
        }

        private void SlideList_MouseMove(object sender, MouseEventArgs e)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // Forçamos explicitamente a utilização do ListBox e DragDrop do WPF
                if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is SlideItem slide)
                {
                    _draggedItem = slide;
                    DataObject dragData = new DataObject("SlideItemFormat", _draggedItem);
                    System.Windows.DragDrop.DoDragDrop(listBox, dragData, DragDropEffects.Copy);
                }
            }
        }

        private void MedleyList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("SlideItemFormat"))
            {
                e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.Copy;
            }
            e.Handled = true;
        }

        private void MedleyList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("SlideItemFormat"))
            {
                if (e.Data.GetData("SlideItemFormat") is SlideItem droppedSlide)
                {
                    // Clona o slide para o Medley
                    MedleySlides.Add(new SlideItem
                    {
                        Header = $"Medley Slide {MedleySlides.Count + 1}",
                        Content = droppedSlide.Content,
                        BackgroundPath = droppedSlide.BackgroundPath
                    });
                }
            }
        }

        // --- CORREÇÃO DO BOTÃO REMOVER NO CENTRO ---
        private void BtnRemoveMedleyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SlideItem slideItem)
            {
                MedleySlides.Remove(slideItem);
            }
        }

        // Mantém também o duplo clique a funcionar como alternativa
        private void LstMedleySlides_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LstMedleySlides.SelectedItem is SlideItem item)
            {
                MedleySlides.Remove(item);
            }
        }
    }
}