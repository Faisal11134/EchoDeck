using System.Windows;

namespace EchoDeck.App.Views;

public partial class InputDialog : Window
{
    public new string Title { get; }
    public string Prompt { get; }
    public string InputText { get; set; }

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        Prompt = prompt;
        InputText = defaultValue;
        DataContext = this;
        InputTextBox.SelectionStart = InputTextBox.Text.Length;
        InputTextBox.Focus();
    }

    private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            DialogResult = true;
            Close();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
