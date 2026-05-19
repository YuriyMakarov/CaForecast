using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace CaForecast.WpfApp;

public partial class MainWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExDlgModalFrame = 0x0001;
    private const uint SwpNosize = 0x0001;
    private const uint SwpNomove = 0x0002;
    private const uint SwpNozorder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += (_, _) => viewModel.CancelPendingWork();
        SourceInitialized += (_, _) => RemoveWindowIcon();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void RemoveWindowIcon()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, extendedStyle | WsExDlgModalFrame);
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpNozorder | SwpFrameChanged);

        const uint wmSetIcon = 0x0080;
        SendMessage(handle, wmSetIcon, IntPtr.Zero, IntPtr.Zero);
        SendMessage(handle, wmSetIcon, new IntPtr(1), IntPtr.Zero);
    }
}
