using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PalCalc.UI.ScreenRecognition
{
    public sealed class PalworldWindowCapture
    {
        private const uint PwRenderFullContent = 0x00000002;
        private const int DwmwaExtendedFrameBounds = 9;
        private const uint SrcCopy = 0x00CC0020;
        private const uint CaptureBlt = 0x40000000;

        public IntPtr FindPalworldWindow()
        {
            var candidates = new List<(IntPtr Handle, long Area)>();

            EnumWindows((handle, _) =>
            {
                if (!IsWindowVisible(handle))
                    return true;

                GetWindowThreadProcessId(handle, out var processId);
                try
                {
                    using var process = Process.GetProcessById((int)processId);
                    var processName = process.ProcessName;
                    if (!processName.Contains("Palworld", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (TryGetWindowBounds(handle, out var bounds))
                        candidates.Add((handle, (long)bounds.Width * bounds.Height));
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }

                return true;
            }, IntPtr.Zero);

            return candidates.OrderByDescending(x => x.Area).Select(x => x.Handle).FirstOrDefault();
        }

        public BitmapSource Capture()
        {
            var handle = FindPalworldWindow();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("실행 중인 Palworld 창을 찾지 못했습니다.");

            if (!TryGetWindowBounds(handle, out var bounds) || bounds.Width <= 0 || bounds.Height <= 0)
                throw new InvalidOperationException("Palworld 창 크기를 확인하지 못했습니다.");

            var windowDc = GetWindowDC(handle);
            if (windowDc == IntPtr.Zero)
                throw new InvalidOperationException("Palworld 화면 장치를 열지 못했습니다.");

            var memoryDc = CreateCompatibleDC(windowDc);
            var bitmap = CreateCompatibleBitmap(windowDc, bounds.Width, bounds.Height);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
                ReleaseDC(handle, windowDc);
                throw new InvalidOperationException("Palworld 화면 캡처 공간을 만들지 못했습니다.");
            }

            var previous = SelectObject(memoryDc, bitmap);

            try
            {
                if (!PrintWindow(handle, memoryDc, PwRenderFullContent))
                {
                    // Some DirectX modes reject PrintWindow. In borderless mode the desktop
                    // framebuffer is a safe read-only fallback while the game is visible.
                    var screenDc = GetDC(IntPtr.Zero);
                    try
                    {
                        if (screenDc == IntPtr.Zero || !BitBlt(
                            memoryDc, 0, 0, bounds.Width, bounds.Height,
                            screenDc, bounds.Left, bounds.Top, SrcCopy | CaptureBlt))
                            throw new InvalidOperationException("Palworld 화면 캡처에 실패했습니다.");
                    }
                    finally
                    {
                        if (screenDc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screenDc);
                    }
                }

                var image = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
                image.Freeze();
                return image;
            }
            finally
            {
                SelectObject(memoryDc, previous);
                DeleteObject(bitmap);
                DeleteDC(memoryDc);
                ReleaseDC(handle, windowDc);
            }
        }

        private static bool TryGetWindowBounds(IntPtr handle, out NativeRect bounds)
        {
            if (DwmGetWindowAttribute(handle, DwmwaExtendedFrameBounds, out bounds, Marshal.SizeOf<NativeRect>()) == 0)
                return true;

            return GetWindowRect(handle, out bounds);
        }

        private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr handle, int attribute, out NativeRect value, int size);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr handle, IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr value);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr value);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr deviceContext);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destination,
            int destinationX,
            int destinationY,
            int width,
            int height,
            IntPtr source,
            int sourceX,
            int sourceY,
            uint operation
        );

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr handle, IntPtr deviceContext, uint flags);
    }
}
