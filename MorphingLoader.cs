using System;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace SircleToSearch;

public sealed class MorphingLoader
{
    private const double SizeRatio = 0.79;

    private readonly Polygon _polygon;
    private readonly double _boxRadius;
    private readonly M3Animator _animator = new();
    private DateTime _startedAt;
    private bool _running;

    public MorphingLoader(Polygon polygon, double radius)
    {
        _polygon = polygon;
        _boxRadius = radius;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _startedAt = DateTime.UtcNow;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var elapsedSeconds = (DateTime.UtcNow - _startedAt).TotalSeconds;
        _animator.Update(elapsedSeconds);

        var shape = M3Shapes.GetMorphedShape(_animator.Morph);
        var rotationRad = _animator.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rotationRad);
        var sin = Math.Sin(rotationRad);
        var scale = _boxRadius * SizeRatio;
        var center = _boxRadius;

        var points = new PointCollection(shape.Length);
        foreach (var p in shape)
        {
            var rx = p.X * cos - p.Y * sin;
            var ry = p.X * sin + p.Y * cos;
            points.Add(new Point(center + rx * scale, center + ry * scale));
        }
        _polygon.Points = points;
    }
}
