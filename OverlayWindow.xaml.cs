using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Bitmap = System.Drawing.Bitmap;
using Rectangle = System.Drawing.Rectangle;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace SircleToSearch;

public partial class OverlayWindow : Window
{
    private const double HandleSize = 14;
    private const double MinSelectionSize = 15;

    private enum DragMode { None, Creating, Moving, Resizing }

    private Bitmap? _screenshot;
    private ResultWindow? _resultWindow;

    private DragMode _mode = DragMode.None;
    private Point _dragAnchor;
    private Rect _selection = Rect.Empty;

    public OverlayWindow()
    {
        InitializeComponent();
        DebugInspector.Attach(this, ignoreNames: ["RootGrid", "ScreenshotImage", "DimOverlay", "AdornerDecorator"]);

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        Opacity = 0;

        Loaded += OverlayWindow_Loaded;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) HandleEscape();
            else if (e.Key == Key.Enter && !_selection.IsEmpty) StartSearch(_selection);
        };
    }

    private void HandleEscape()
    {
        if (_resultWindow?.IsResultVisible == true)
            _resultWindow.HideResult();
        else
            Close();
    }

    private void OverlayWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        var (bitmap, _) = ScreenCapture.CaptureVirtualScreen();
        _screenshot = bitmap;

        ScreenshotImage.Source = ToBitmapSource(bitmap);
        UpdateDimOverlay(Rect.Empty);
        HintText.Text = Strings.Get("OverlayHint");

        Activate();
        Focus();
        NativeMethods.ForceRepaint(this);

        Opacity = 0;
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

        _resultWindow = new ResultWindow();
        _resultWindow.Closed += (_, _) => _resultWindow = null;
        _resultWindow.EscapeRequested += HandleEscape;
        _resultWindow.Show();
        _ = _resultWindow.PreWarmAsync();

        var pulse = new DoubleAnimation(0.9, 1, TimeSpan.FromSeconds(1.4))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        HintText.BeginAnimation(OpacityProperty, pulse);
    }

    private void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(RootGrid);

        var oppositeCorner = HitCorner(pos);
        if (oppositeCorner.HasValue)
        {
            _mode = DragMode.Resizing;
            _dragAnchor = oppositeCorner.Value;
        }
        else if (!_selection.IsEmpty && _selection.Contains(pos))
        {
            _mode = DragMode.Moving;
            _dragAnchor = new Point(pos.X - _selection.X, pos.Y - _selection.Y);
        }
        else
        {
            _mode = DragMode.Creating;
            _dragAnchor = pos;
            _selection = new Rect(pos, pos);
        }

        RootGrid.CaptureMouse();
        UpdateSelectionVisuals();
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_mode == DragMode.None) return;

        var pos = e.GetPosition(RootGrid);
        pos.X = Math.Clamp(pos.X, 0, ActualWidth);
        pos.Y = Math.Clamp(pos.Y, 0, ActualHeight);

        switch (_mode)
        {
            case DragMode.Creating:
            case DragMode.Resizing:
                var candidate = new Rect(
                    Math.Min(_dragAnchor.X, pos.X), Math.Min(_dragAnchor.Y, pos.Y),
                    Math.Abs(pos.X - _dragAnchor.X), Math.Abs(pos.Y - _dragAnchor.Y));
                if (_mode == DragMode.Creating || (candidate.Width >= MinSelectionSize && candidate.Height >= MinSelectionSize))
                    _selection = candidate;
                break;

            case DragMode.Moving:
                var width = _selection.Width;
                var height = _selection.Height;
                var newX = Math.Clamp(pos.X - _dragAnchor.X, 0, Math.Max(0, ActualWidth - width));
                var newY = Math.Clamp(pos.Y - _dragAnchor.Y, 0, Math.Max(0, ActualHeight - height));
                _selection = new Rect(newX, newY, width, height);
                break;
        }

        UpdateSelectionVisuals();
    }

    private void RootGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode == DragMode.None) return;
        var mode = _mode;
        _mode = DragMode.None;
        RootGrid.ReleaseMouseCapture();
        FinishDrag(mode);
    }

    private void RootGrid_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_mode == DragMode.None) return;
        var mode = _mode;
        _mode = DragMode.None;
        FinishDrag(mode);
    }

    private void FinishDrag(DragMode mode)
    {
        if (_selection.Width < MinSelectionSize || _selection.Height < MinSelectionSize)
        {
            if (mode == DragMode.Creating)
            {
                _selection = Rect.Empty;
                UpdateSelectionVisuals();
            }
            return;
        }

        StartSearch(_selection);
    }

    private Point? HitCorner(Point pos)
    {
        if (_selection.IsEmpty) return null;

        var corners = new (Point Corner, Point Opposite)[]
        {
            (new Point(_selection.Left, _selection.Top), new Point(_selection.Right, _selection.Bottom)),
            (new Point(_selection.Right, _selection.Top), new Point(_selection.Left, _selection.Bottom)),
            (new Point(_selection.Left, _selection.Bottom), new Point(_selection.Right, _selection.Top)),
            (new Point(_selection.Right, _selection.Bottom), new Point(_selection.Left, _selection.Top)),
        };

        foreach (var (corner, opposite) in corners)
        {
            if ((corner - pos).Length <= HandleSize)
                return opposite;
        }
        return null;
    }

    private void UpdateSelectionVisuals()
    {
        UpdateDimOverlay(_selection);

        if (_selection.IsEmpty || _selection.Width <= 0 || _selection.Height <= 0)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            Handles.Children.Clear();
            return;
        }

        var config = SelectionVisualConfig.FromSettings(AppSettings.Current);
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionRenderer.ApplyGlow(SelectionBorder, _selection, config);
        SelectionRenderer.DrawBrackets(Handles, _selection, config);
    }

    private void UpdateDimOverlay(Rect selection)
    {
        var bounds = new Rect(0, 0, Math.Max(ActualWidth, 1), Math.Max(ActualHeight, 1));
        if (selection.IsEmpty || selection.Width <= 0 || selection.Height <= 0)
        {
            DimOverlay.Data = new RectangleGeometry(bounds);
            return;
        }

        DimOverlay.Data = SelectionRenderer.BuildDimHole(bounds, selection, AppSettings.Current.SelectionRadius);
    }

    private void StartSearch(Rect selection)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var cropped = CropToBounds(selection);
            var jpegBytes = ToJpegBytes(cropped);
            cropped.Dispose();
            AppLog.Info($"[perf] Crop+encode: {sw.ElapsedMilliseconds}ms");

            if (_resultWindow is null)
            {
                _resultWindow = new ResultWindow();
                _resultWindow.Closed += (_, _) => _resultWindow = null;
                _resultWindow.EscapeRequested += HandleEscape;
                _resultWindow.Show();
            }
            _resultWindow.ShowSearch(jpegBytes);
        }
        catch (Exception ex)
        {
            AppLog.Error("Поиск не удался", ex);
        }
    }

    private Bitmap CropToBounds(Rect dipBounds)
    {
        var screenshot = _screenshot!;

        var scaleX = screenshot.Width / ActualWidth;
        var scaleY = screenshot.Height / ActualHeight;

        var pixelRect = new Rectangle(
            (int)(dipBounds.X * scaleX),
            (int)(dipBounds.Y * scaleY),
            (int)(dipBounds.Width * scaleX),
            (int)(dipBounds.Height * scaleY));

        pixelRect.Intersect(new Rectangle(0, 0, screenshot.Width, screenshot.Height));
        if (pixelRect.Width <= 0 || pixelRect.Height <= 0)
        {
            pixelRect = new Rectangle(0, 0, screenshot.Width, screenshot.Height);
        }

        var result = new Bitmap(pixelRect.Width, pixelRect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(screenshot, new Rectangle(0, 0, pixelRect.Width, pixelRect.Height),
                pixelRect, GraphicsUnit.Pixel);
        }
        return result;
    }

    private static byte[] ToJpegBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Jpeg);
        return ms.ToArray();
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _screenshot?.Dispose();
        _resultWindow?.Close();
        base.OnClosed(e);
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, uint flags);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    public static void ForceRepaint(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }
}
