using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DotaHighlights.Client.Triggers;

/// <summary>
/// Gatillo manual por hotkey global (por defecto F9). Registra la tecla a nivel
/// de sistema, así funciona aunque Dota 2 tenga el foco.
/// </summary>
public sealed class HotkeyTrigger : IHighlightTrigger
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xB001;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly uint _modifiers;
    private readonly uint _virtualKey;
    private HwndSource? _source;
    private bool _registered;

    public event EventHandler<HighlightTriggeredEventArgs>? Triggered;

    /// <param name="windowHandle">HWND de la ventana principal (para recibir el mensaje).</param>
    /// <param name="virtualKey">Virtual-key code. 0x78 = F9.</param>
    /// <param name="modifiers">Modificadores (0 = ninguno).</param>
    public HotkeyTrigger(IntPtr windowHandle, uint virtualKey = 0x78, uint modifiers = 0)
    {
        _hwnd = windowHandle;
        _virtualKey = virtualKey;
        _modifiers = modifiers;
    }

    public void Start()
    {
        if (_registered) return;
        _source = HwndSource.FromHwnd(_hwnd)
            ?? throw new InvalidOperationException("No se pudo obtener el HwndSource de la ventana.");
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(_hwnd, HotkeyId, _modifiers, _virtualKey);
        if (!_registered)
            throw new InvalidOperationException("No se pudo registrar el hotkey (¿ya lo usa otra app?).");
    }

    public void Stop()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Triggered?.Invoke(this, new HighlightTriggeredEventArgs("Hotkey F9"));
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
