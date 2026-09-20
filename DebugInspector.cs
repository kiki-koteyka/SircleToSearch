using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Border = System.Windows.Controls.Border;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;
using Control = System.Windows.Controls.Control;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace SircleToSearch;

public static class DebugInspector
{
    public static void Attach(Window window, string[]? ignoreNames = null)
    {
        var ignored = ignoreNames is null ? [] : new System.Collections.Generic.HashSet<string>(ignoreNames);

        if (window.Content is UIElement original && AdornerLayer.GetAdornerLayer(original) is null)
        {
            window.Content = null;
            window.Content = new AdornerDecorator { Child = original };
        }

        var active = false;
        InspectorAdorner? adorner = null;

        void OnMove(object? s, MouseEventArgs e)
        {
            if (window.Content is not UIElement root || adorner is null) return;

            try
            {
                OnMoveCore(root, adorner, e);
            }
            catch
            {
                adorner.ShowHint();
            }
        }

        void OnMoveCore(UIElement root, InspectorAdorner adorner, MouseEventArgs e)
        {
            var pos = e.GetPosition(root);
            var hit = VisualTreeHelper.HitTest(root, pos)?.VisualHit;

            DependencyObject? node = hit;
            FrameworkElement? fe = null;
            while (node is not null)
            {
                if (node is FrameworkElement candidate
                    && !ignored.Contains(candidate.Name)
                    && !ignored.Contains(candidate.GetType().Name))
                {
                    fe = candidate;
                    break;
                }
                node = VisualTreeHelper.GetParent(node);
            }

            if (fe is null || fe.ActualWidth <= 0 || fe.ActualHeight <= 0)
            {
                adorner.ShowHint();
                return;
            }

            Rect bounds;
            try
            {
                bounds = fe.TransformToAncestor(root)
                    .TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
            }
            catch (InvalidOperationException)
            {
                adorner.ShowHint();
                return;
            }
            adorner.Update(bounds, Describe(fe));
        }

        void OnClick(object? s, MouseButtonEventArgs e)
        {
            if (adorner?.CurrentInfo is not { } info) return;
            try { System.Windows.Clipboard.SetText(info); } catch { }
            adorner.FlashCopied();
            e.Handled = true;
        }

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.F9) return;
            active = !active;

            if (active)
            {
                if (window.Content is UIElement root)
                {
                    var layer = AdornerLayer.GetAdornerLayer(root);
                    if (layer is not null)
                    {
                        adorner = new InspectorAdorner(root);
                        layer.Add(adorner);
                        adorner.ShowHint();
                    }
                }
                window.PreviewMouseMove += OnMove;
                window.PreviewMouseLeftButtonDown += OnClick;
            }
            else
            {
                window.PreviewMouseMove -= OnMove;
                window.PreviewMouseLeftButtonDown -= OnClick;
                if (adorner is not null && window.Content is UIElement root2)
                {
                    AdornerLayer.GetAdornerLayer(root2)?.Remove(adorner);
                    adorner = null;
                }
            }
        };
    }

    private static string Describe(FrameworkElement fe)
    {
        var typeName = fe.GetType().Name;
        var name = string.IsNullOrEmpty(fe.Name) ? "" : $" x:Name=\"{fe.Name}\"";
        var size = $"{fe.ActualWidth:0}x{fe.ActualHeight:0}";

        var brush = fe switch
        {
            Shape shape => shape.Fill as SolidColorBrush,
            Border b => b.Background as SolidColorBrush,
            Control ctl => ctl.Background as SolidColorBrush,
            TextBlock tb => tb.Foreground as SolidColorBrush,
            _ => null,
        };
        var colorInfo = brush is not null ? $" color={brush.Color}" : "";

        return $"{typeName}{name} {size}{colorInfo}";
    }
}

internal sealed class InspectorAdorner : Adorner
{
    private Rect _bounds = Rect.Empty;
    private string _label = "";
    private bool _flash;
    private bool _hint;

    public string? CurrentInfo { get; private set; }

    public InspectorAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override System.Windows.Media.HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;

    public void Update(Rect bounds, string info)
    {
        _bounds = bounds;
        _label = info;
        CurrentInfo = info;
        _flash = false;
        _hint = false;
        InvalidateVisual();
    }

    public void ShowHint()
    {
        _bounds = Rect.Empty;
        CurrentInfo = null;
        _hint = true;
        InvalidateVisual();
    }

    public void FlashCopied()
    {
        _flash = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var accent = AccentColor.Parse(AppSettings.Current.AccentColor);
        var lineColor = _flash ? Colors.LimeGreen : accent;

        if (_hint)
        {
            DrawLabel(dc, new Point(12, 12), "F9: debug inspector on - hover an element, click to copy");
            return;
        }

        if (_bounds.IsEmpty) return;

        var pen = new Pen(new SolidColorBrush(lineColor), 2);
        dc.DrawRectangle(
            new SolidColorBrush(Color.FromArgb(0x22, lineColor.R, lineColor.G, lineColor.B)),
            pen, _bounds);

        var labelText = _flash ? $"{_label}  (copied)" : _label;
        var labelPos = new Point(_bounds.Left, Math.Max(0, _bounds.Top - 24));
        DrawLabel(dc, labelPos, labelText);
    }

    private void DrawLabel(DrawingContext dc, Point at, string text)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight,
            new Typeface("Consolas"), 12, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var rect = new Rect(at, new Size(formatted.Width + 12, formatted.Height + 6));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0x20, 0x21, 0x24)), null, rect, 4, 4);
        dc.DrawText(formatted, new Point(at.X + 6, at.Y + 3));
    }
}
