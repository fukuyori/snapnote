using System.Windows;
using System.Windows.Controls;
using SnapNoteStudio.Services;

namespace SnapNoteStudio.Views;

public partial class TextInputDialog : Window
{
    public string InputText => InputTextBox.Text;
    
    public double SelectedFontSize
    {
        get
        {
            if (FontSizeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string sizeStr)
            {
                return double.Parse(sizeStr);
            }
            return 18;
        }
    }

    public TextInputDialog()
    {
        InitializeComponent();
        ApplyLocalization();
        Loaded += (s, e) => InputTextBox.Focus();
    }
    
    private void ApplyLocalization()
    {
        Title = L10n.Get("EnterText").TrimEnd(':');
        PromptText.Text = L10n.Get("EnterText");
        FontSizeLabel.Text = L10n.Get("FontSize");
        CancelButton.Content = L10n.Get("Cancel");
        OKButton.Content = L10n.Get("OK");
    }

    private void OK_Click(object sender, RoutedEventArgs e)
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
