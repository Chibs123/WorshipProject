using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WorshipProjectionApp.Models;
using WorshipProjectionApp.Services;

namespace WorshipProjectionApp
{
    public partial class SongEditorWindow : Window
    {
        public Song? ResultSong { get; private set; }

        private ObservableCollection<SlideItem> _slides = new();
        private Song? _originalSong; // Guarda a música original para não perder o ID
        private List<MediaBackground> _galleryBackgrounds = new();

        public SongEditorWindow(Song? songToEdit = null)
        {
            InitializeComponent();
            _originalSong = songToEdit;
            LstSlidesEditor.ItemsSource = _slides;

            // 1. Carrega a Galeria de Fundos Interna para a ComboBox
            _galleryBackgrounds = new BackgroundGalleryService().GetBackgrounds();
            CmbBackgrounds.ItemsSource = _galleryBackgrounds;

            // 2. Carrega a Música
            if (_originalSong != null)
            {
                TxtTitle.Text = _originalSong.Title;
                TxtArtist.Text = _originalSong.Artist;

                var loadedSlides = _originalSong.ParseSlides();
                foreach (var slide in loadedSlides)
                {
                    _slides.Add(slide);
                }
            }

            if (_slides.Count == 0)
            {
                _slides.Add(new SlideItem { Header = "Slide 1", Content = "" });
            }

            LstSlidesEditor.SelectedIndex = 0;
        }

        private void LstSlidesEditor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSlidesEditor.SelectedItem is SlideItem slide)
            {
                EditorPanel.IsEnabled = true;

                // Desliga os eventos para não subscrever dados enquanto carrega
                TxtSlideHeader.TextChanged -= TxtSlideContent_TextChanged;
                TxtSlideContent.TextChanged -= TxtSlideContent_TextChanged;
                CmbBackgrounds.SelectionChanged -= CmbBackgrounds_SelectionChanged;

                TxtSlideHeader.Text = slide.Header;
                TxtSlideContent.Text = slide.Content;

                // Seleciona na combobox o fundo atual do slide
                if (!string.IsNullOrEmpty(slide.BackgroundPath))
                {
                    CmbBackgrounds.SelectedItem = _galleryBackgrounds.FirstOrDefault(b => b.FilePath == slide.BackgroundPath);
                }
                else
                {
                    CmbBackgrounds.SelectedItem = null;
                }

                // Liga os eventos de novo
                TxtSlideHeader.TextChanged += TxtSlideContent_TextChanged;
                TxtSlideContent.TextChanged += TxtSlideContent_TextChanged;
                CmbBackgrounds.SelectionChanged += CmbBackgrounds_SelectionChanged;
            }
            else
            {
                EditorPanel.IsEnabled = false;
                TxtSlideHeader.Text = "";
                TxtSlideContent.Text = "";
                CmbBackgrounds.SelectedItem = null;
            }
        }

        private void TxtSlideContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LstSlidesEditor.SelectedItem is SlideItem slide)
            {
                slide.Header = TxtSlideHeader.Text;
                slide.Content = TxtSlideContent.Text;
            }
        }

        // Quando o utilizador muda o fundo na ComboBox, atualiza o Slide selecionado
        private void CmbBackgrounds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSlidesEditor.SelectedItem is SlideItem slide)
            {
                if (CmbBackgrounds.SelectedItem is MediaBackground bg)
                {
                    slide.BackgroundPath = bg.FilePath;
                }
                else
                {
                    slide.BackgroundPath = null;
                }
            }
        }

        private void BtnAddSlide_Click(object sender, RoutedEventArgs e)
        {
            var newSlide = new SlideItem { Header = $"Slide {_slides.Count + 1}", Content = "" };
            _slides.Add(newSlide);
            LstSlidesEditor.SelectedItem = newSlide;
            TxtSlideContent.Focus();
        }

        private void BtnRemoveSlide_Click(object sender, RoutedEventArgs e)
        {
            if (LstSlidesEditor.SelectedItem is SlideItem slide)
            {
                int index = LstSlidesEditor.SelectedIndex;
                _slides.Remove(slide);

                if (_slides.Count > 0)
                {
                    LstSlidesEditor.SelectedIndex = Math.Min(index, _slides.Count - 1);
                }
            }
        }

        private void BtnClearBackground_Click(object sender, RoutedEventArgs e)
        {
            CmbBackgrounds.SelectedItem = null; // Isto vai ativar o SelectionChanged e limpar o BackgroundPath
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                System.Windows.MessageBox.Show("A música precisa de um título.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Se a música já existia, atualiza as propriedades, assim o ID não se perde!
            if (_originalSong != null)
            {
                _originalSong.Title = TxtTitle.Text;
                _originalSong.Artist = TxtArtist.Text;
                _originalSong.RawLyrics = JsonSerializer.Serialize(_slides);
                ResultSong = _originalSong;
            }
            else
            {
                // Se for música nova, cria do zero
                ResultSong = new Song
                {
                    Title = TxtTitle.Text,
                    Artist = TxtArtist.Text,
                    RawLyrics = JsonSerializer.Serialize(_slides)
                };
            }

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}