using System.Windows;
using System.Windows.Controls;

namespace SnapNoteStudio.Views;

public partial class ResizeDialog : Window
{
    private readonly int _originalWidth;
    private readonly int _originalHeight;
    private readonly double _aspectRatio;
    private bool _isUpdating;

    public int NewWidth { get; private set; }
    public int NewHeight { get; private set; }

    public ResizeDialog(int originalWidth, int originalHeight)
    {
        InitializeComponent();
        
        _originalWidth = originalWidth;
        _originalHeight = originalHeight;
        _aspectRatio = (double)originalWidth / originalHeight;
        
        WidthTextBox.Text = originalWidth.ToString();
        HeightTextBox.Text = originalHeight.ToString();
        OriginalSizeText.Text = $"元のサイズ: {originalWidth} × {originalHeight} px";
        
        NewWidth = originalWidth;
        NewHeight = originalHeight;
    }

    private void WidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        
        if (int.TryParse(WidthTextBox.Text, out int width) && width > 0)
        {
            NewWidth = width;
            
            if (KeepAspectRatioCheckBox.IsChecked == true)
            {
                _isUpdating = true;
                NewHeight = (int)(width / _aspectRatio);
                HeightTextBox.Text = NewHeight.ToString();
                _isUpdating = false;
            }
        }
    }

    private void HeightTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        
        if (int.TryParse(HeightTextBox.Text, out int height) && height > 0)
        {
            NewHeight = height;
            
            if (KeepAspectRatioCheckBox.IsChecked == true)
            {
                _isUpdating = true;
                NewWidth = (int)(height * _aspectRatio);
                WidthTextBox.Text = NewWidth.ToString();
                _isUpdating = false;
            }
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(WidthTextBox.Text, out int width) && width > 0 &&
            int.TryParse(HeightTextBox.Text, out int height) && height > 0)
        {
            NewWidth = width;
            NewHeight = height;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("有効な数値を入力してください", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
