using System.Windows;
using System.Windows.Interop;
using DotaHighlights.Client.Triggers;
using DotaHighlights.Client.ViewModels;

namespace DotaHighlights.Client;

public partial class MainWindow : Window
{
    private readonly MainViewModel _captureVm;
    private HotkeyTrigger? _hotkey;

    public MainWindow(ShellViewModel shell, MainViewModel captureVm)
    {
        InitializeComponent();
        _captureVm = captureVm;
        DataContext = shell;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Hotkey global F9 (guardado manual de respaldo), atado a la ventana.
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkey = new HotkeyTrigger(hwnd, virtualKey: 0x78 /* F9 */);
        _hotkey.Triggered += (_, ev) => _captureVm.TriggerSave(ev.Reason);
        try
        {
            _hotkey.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo registrar el atajo F9: {ex.Message}",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkey?.Dispose();
        base.OnClosed(e);
    }
}
