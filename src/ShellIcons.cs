using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>Echte Symbolbilder der Desktopelemente über die Shell (IShellItemImageFactory), zwischengespeichert.</summary>
static class ShellIcons
{
    static readonly Dictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Symbol zu einem Shell-Analysenamen (Pfad oder ::{CLSID}); null, wenn die Shell keins liefert.</summary>
    public static ImageSource? Get(string parsingName, int size = 64)
    {
        if (cache.TryGetValue(parsingName, out var hit)) return hit;
        ImageSource? image = null;
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var item) == 0 && item is IShellItemImageFactory factory)
            {
                // 0x1 größer erlaubt, 0x4 nur Symbol (keine Vorschau großer Bilder)
                if (factory.GetImage(new NativeSize { Cx = size, Cy = size }, 0x1 | 0x4, out bitmap) == 0 && bitmap != IntPtr.Zero) image = Convert(bitmap);
                Marshal.ReleaseComObject(item);
            }
        }
        catch { image = null; }
        finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); }
        cache[parsingName] = image;
        return image;
    }

    /// <summary>Anzeigename aus dem Analysenamen, wenn kein Explorer-Name vorliegt (Datei ohne Endung).</summary>
    public static string NameOf(string parsingName) => parsingName.StartsWith("::", StringComparison.Ordinal) ? "" : Path.GetFileNameWithoutExtension(parsingName);

    /// <summary>32-Bit-DIB (vormultipliziertes BGRA, unten beginnend) → BitmapSource mit Transparenz.</summary>
    static BitmapSource? Convert(IntPtr hbitmap)
    {
        var info = new BitmapInfo();
        if (GetObject(hbitmap, Marshal.SizeOf<BitmapInfo>(), ref info) == 0 || info.Bits == IntPtr.Zero || info.BitsPixel != 32) return null;
        int w = info.Width, h = Math.Abs(info.Height), stride = info.WidthBytes;
        var pixels = new byte[stride * h];
        Marshal.Copy(info.Bits, pixels, 0, pixels.Length);
        var flipped = new byte[pixels.Length];
        for (int y = 0; y < h; y++) Buffer.BlockCopy(pixels, (h - 1 - y) * stride, flipped, y * stride, stride);
        var source = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, flipped, stride);
        source.Freeze();
        return source;
    }

    [StructLayout(LayoutKind.Sequential)] struct NativeSize { public int Cx, Cy; }
    [StructLayout(LayoutKind.Sequential)]
    struct BitmapInfo { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory { [PreserveSig] int GetImage(NativeSize size, int flags, out IntPtr bitmap); }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object item);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr handle, int size, ref BitmapInfo info);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
}
