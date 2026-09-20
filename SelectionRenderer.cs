using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;

namespace SircleToSearch;

public sealed class SelectionVisualConfig
{
    public double Hug { get; set; }
    public double CornerRadius { get; set; }
    public double ArmLength { get; set; }
    public double Thickness { get; set; }
    public double GlowGap { get; set; }
    public double GlowThickness { get; set; }
    public double GlowBlur { get; set; }
    public double GlowOpacity { get; set; }
    public double SelectionRadius { get; set; }
    public Color Accent { get; set; } = Color.FromRgb(0x00, 0x9F, 0xAA);

    public static SelectionVisualConfig FromSettings(AppSettings s) => new()
    {
        Hug = s.SelectionHug,
        CornerRadius = s.SelectionCornerRadius,
        ArmLength = s.SelectionArmLength,
        Thickness = s.SelectionThickness,
        GlowGap = s.SelectionGlowGap,
        GlowThickness = s.SelectionGlowThickness,
        GlowBlur = s.SelectionGlowBlur,
        GlowOpacity = s.SelectionGlowOpacity,
        SelectionRadius = s.SelectionRadius,
        Accent = AccentColor.Parse(s.AccentColor),
    };
}

public static class SelectionRenderer
{
    public static void ApplyGlow(System.Windows.Shapes.Rectangle glow, Rect selection, SelectionVisualConfig c)
    {
        Canvas.SetLeft(glow, selection.X - c.GlowGap);
        Canvas.SetTop(glow, selection.Y - c.GlowGap);
        glow.Width = selection.Width + c.GlowGap * 2;
        glow.Height = selection.Height + c.GlowGap * 2;
        glow.RadiusX = c.SelectionRadius;
        glow.RadiusY = c.SelectionRadius;
        glow.StrokeThickness = c.GlowThickness;
        glow.Stroke = new SolidColorBrush(Color.FromArgb(
            (byte)(c.GlowOpacity / 100.0 * 255), c.Accent.R, c.Accent.G, c.Accent.B));
        glow.Effect = new BlurEffect { Radius = c.GlowBlur };

        var bleed = c.GlowThickness * 4;
        var outerLocal = new Rect(-bleed, -bleed, glow.Width + bleed * 2, glow.Height + bleed * 2);
        var innerLocal = new Rect(c.GlowGap, c.GlowGap, selection.Width, selection.Height);
        glow.Clip = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(outerLocal),
            new RectangleGeometry(innerLocal, c.SelectionRadius, c.SelectionRadius));
    }

    public static void DrawBrackets(Canvas host, Rect rect, SelectionVisualConfig c)
    {
        host.Children.Clear();

        var bracketBrush = new SolidColorBrush(c.Accent);

        void AddBracket(Point p, int dx, int dy)
        {
            var radius = System.Math.Max(0.0001, System.Math.Min(c.CornerRadius, c.ArmLength));
            var figure = new System.Windows.Media.PathFigure { StartPoint = new Point(p.X + dx * c.ArmLength, p.Y) };
            figure.Segments.Add(new System.Windows.Media.LineSegment(new Point(p.X + dx * radius, p.Y), true));
            figure.Segments.Add(new System.Windows.Media.ArcSegment(
                new Point(p.X, p.Y + dy * radius),
                new Size(radius, radius),
                0, false,
                dx * dy > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise,
                true));
            figure.Segments.Add(new System.Windows.Media.LineSegment(new Point(p.X, p.Y + dy * c.ArmLength), true));

            var geometry = new System.Windows.Media.PathGeometry();
            geometry.Figures.Add(figure);

            var path = new System.Windows.Shapes.Path
            {
                Data = geometry,
                Stroke = bracketBrush,
                StrokeThickness = c.Thickness,
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
            };
            host.Children.Add(path);
        }

        AddBracket(new Point(rect.Left - c.Hug, rect.Top - c.Hug), 1, 1);
        AddBracket(new Point(rect.Right + c.Hug, rect.Top - c.Hug), -1, 1);
        AddBracket(new Point(rect.Left - c.Hug, rect.Bottom + c.Hug), 1, -1);
        AddBracket(new Point(rect.Right + c.Hug, rect.Bottom + c.Hug), -1, -1);
    }

    public static Geometry BuildDimHole(Rect bounds, Rect selection, double radius)
    {
        var full = new RectangleGeometry(bounds);
        var hole = new RectangleGeometry(selection, radius, radius);
        return new CombinedGeometry(GeometryCombineMode.Exclude, full, hole);
    }
}
