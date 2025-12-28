using System.Windows;
using System.Windows.Controls;

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
        Loaded += (s, e) => InputTextBox.Focus();
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
