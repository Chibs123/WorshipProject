using System.Windows;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp;

public partial class SongEditorWindow : Window
{
    public Song? ResultSong { get; private set; }

    public SongEditorWindow(Song? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            TxtTitle.Text = existing.Title;
            TxtArtist.Text = existing.Artist;
            TxtLyrics.Text = existing.RawLyrics;
            ResultSong = existing;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtTitle.Text))
        {
            System.Windows.MessageBox.Show("Por favor indica o título da canção.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultSong ??= new Song();
        ResultSong.Title = TxtTitle.Text.Trim();
        ResultSong.Artist = TxtArtist.Text.Trim();
        ResultSong.RawLyrics = TxtLyrics.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}