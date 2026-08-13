using System.Windows;
using System.Windows.Interop;
using DotaHighlights.Client.Triggers;
using DotaHighlights.Client.ViewModels;

namespace DotaHighlights.Client;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private HotkeyTrigger? _hotkey;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Registra el hotkey global F9 como primer IHighlightTrigger (Fase 1).
        var hwnd = new WindowInteropHelper(this).Handle;
        _hotkey = new HotkeyTrigger(hwnd, virtualKey: 0x78 /* F9 */);
        _hotkey.Triggered += (_, _) => _vm.TriggerSave();
        try
        {
            _hotkey.Start();
        }
        catch (Exception ex)
        {
            // No es fatal: el botón "Guardar highlight" sigue funcionando.
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
