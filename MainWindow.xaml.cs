using System.Windows;
using System.Windows.Input;

namespace WpfSortableCardLayoutsDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        HorizontalBoard.ResetDemo();
        VerticalBoard.ResetDemo();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        bool handled = HorizontalBoard.CancelActiveDrag() | VerticalBoard.CancelActiveDrag();
        e.Handled = handled;
    }
}
