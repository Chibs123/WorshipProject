using System.Windows;

namespace WorshipProjectionApp;

public partial class AlertWindow : Window
{
    public string MessageText { get; private set; } = string.Empty;
    public bool ShowClock { get; private set; }
    public int CountdownMinutes { get; private set; }
    public bool ShouldHide { get; private set; }

    public AlertWindow()
    {
        InitializeComponent();
    }

    private void BtnShow_Click(object sender, RoutedEventArgs e)
    {
        MessageText = TxtAlertMessage.Text.Trim();
        ShowClock = RbClock.IsChecked == true;

        if (RbCountdown.IsChecked == true && int.TryParse(TxtMinutes.Text, out int mins))
        {
            CountdownMinutes = mins;
        }

        ShouldHide = false;
        DialogResult = true;
        Close();
    }

    private void BtnHide_Click(object sender, RoutedEventArgs e)
    {
        ShouldHide = true;
        DialogResult = true;
        Close();
    }
}