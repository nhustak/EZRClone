using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EZRClone;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitleBar(true);
    }

    private void ApplyDarkTitleBar(bool dark)
    {
        if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwndSource.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
    }
}
