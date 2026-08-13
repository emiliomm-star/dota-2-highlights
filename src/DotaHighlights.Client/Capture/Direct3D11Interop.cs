using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace DotaHighlights.Client.Capture;

/// <summary>
/// Puentes de interoperabilidad entre Direct3D 11 (Vortice) y las APIs WinRT de
/// Windows.Graphics.Capture. Necesario porque WGC trabaja con IDirect3DDevice /
/// IDirect3DSurface, no directamente con los tipos de D3D11.
/// </summary>
internal static class Direct3D11Interop
{
    private static readonly Guid ID3D11Texture2D_IID =
        new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    private static readonly Guid GraphicsCaptureItem_IID =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface([In] ref Guid iid);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    /// <summary>Crea un IDirect3DDevice (WinRT) a partir de un ID3D11Device (Vortice).</summary>
    public static IDirect3DDevice CreateWinRtDevice(ID3D11Device device)
    {
        using var dxgi = device.QueryInterface<IDXGIDevice>();
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out IntPtr graphicsDevicePtr);
        if (hr != 0)
            throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice falló (HRESULT 0x{hr:X8}).");
        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevicePtr);
        }
        finally
        {
            Marshal.Release(graphicsDevicePtr);
        }
    }

    /// <summary>Obtiene la ID3D11Texture2D subyacente de una superficie de captura WinRT.</summary>
    public static ID3D11Texture2D GetTexture(IDirect3DSurface surface)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        Guid iid = ID3D11Texture2D_IID;
        IntPtr texturePtr = access.GetInterface(ref iid);
        return new ID3D11Texture2D(texturePtr);
    }

    /// <summary>Crea un GraphicsCaptureItem para un monitor concreto (HMONITOR).</summary>
    public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hmonitor)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        Guid iid = GraphicsCaptureItem_IID;
        IntPtr itemPtr = interop.CreateForMonitor(hmonitor, ref iid);
        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPtr);
        }
        finally
        {
            Marshal.Release(itemPtr);
        }
    }

    /// <summary>Crea un GraphicsCaptureItem para una ventana concreta (HWND).</summary>
    public static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        Guid iid = GraphicsCaptureItem_IID;
        IntPtr itemPtr = interop.CreateForWindow(hwnd, ref iid);
        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPtr);
        }
        finally
        {
            Marshal.Release(itemPtr);
        }
    }
}
