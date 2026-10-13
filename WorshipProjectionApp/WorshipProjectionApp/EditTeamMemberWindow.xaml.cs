using System.Windows;
using WorshipProjectionApp.Models;

namespace WorshipProjectionApp;

public partial class EditTeamMemberWindow : Window
{
    public WorshipTeamMember Member { get; private set; }
    public bool IsDeleted { get; private set; }

    public EditTeamMemberWindow(WorshipTeamMember member)
    {
        InitializeComponent();
        Member = member;

        TxtName.Text = member.Name;
        TxtRole.Text = member.Role;
        TxtMic.Text = member.Microphone;
        TxtReturn.Text = member.InEarReturn;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            System.Windows.MessageBox.Show("O nome não pode estar vazio.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Member.Name = TxtName.Text.Trim();
        Member.Role = TxtRole.Text.Trim();
        Member.Microphone = TxtMic.Text.Trim();
        Member.InEarReturn = TxtReturn.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        IsDeleted = true;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}