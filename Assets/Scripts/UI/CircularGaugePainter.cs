using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Attaches to any VisualElement via RegisterPainter() and draws
/// a circular arc fill using generateVisualContent.
///
/// Usage:
///   var painter = new CircularGaugePainter(arcElement);
///   painter.SetPercent(0.65f);   // 0-1 range
/// </summary>
public class CircularGaugePainter
{
    // ── colours ─────────────────────────────────────────────────
    private static readonly Color TrackColor    = new Color(0.102f, 0.173f, 0.251f, 0.90f);  // dim slate
    private static readonly Color FillColorOk   = new Color(0.239f, 0.659f, 0.812f, 1f);    // cyan-blue
    private static readonly Color FillColorWarn = new Color(1.000f, 0.596f, 0.000f, 1f);    // amber
    private static readonly Color FillColorDanger = new Color(0.878f, 0.361f, 0.361f, 1f);  // red

    // ── geometry constants ───────────────────────────────────────
    private const float StartAngleDeg = -230f;   // bottom-left
    private const float SweepDeg      =  280f;   // total sweep
    private const float StrokeWidth   =   9f;    // ring thickness (px)
    private const int   Segments      =  64;     // smoothness

    // ── state ────────────────────────────────────────────────────
    private readonly VisualElement _el;
    private float _percent = 0f;   // 0-1

    // ── ctor ────────────────────────────────────────────────────
    public CircularGaugePainter(VisualElement el)
    {
        _el = el;
        _el.generateVisualContent += OnGenerateVisualContent;
    }

    public void Detach()
    {
        _el.generateVisualContent -= OnGenerateVisualContent;
    }

    // Call with 0-100 value
    public void SetPercent(float pct0to100)
    {
        _percent = Mathf.Clamp01(pct0to100 / 100f);
        _el.MarkDirtyRepaint();
    }

    // ── paint ────────────────────────────────────────────────────
    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        var r = _el.contentRect;
        float cx = r.width  * 0.5f;
        float cy = r.height * 0.5f;
        float radius = Mathf.Min(cx, cy) - StrokeWidth * 0.5f;
        if (radius <= 0f) return;

        var painter = ctx.painter2D;

        // 1. track (background ring)
        DrawArc(painter, cx, cy, radius, StartAngleDeg,
                StartAngleDeg + SweepDeg, TrackColor);

        // 2. fill (foreground arc up to current %)
        if (_percent > 0.001f)
        {
            Color fillColor = _percent < 0.70f ? FillColorOk
                            : _percent < 0.90f ? FillColorWarn
                            : FillColorDanger;

            DrawArc(painter, cx, cy, radius, StartAngleDeg,
                    StartAngleDeg + SweepDeg * _percent, fillColor);
        }
    }

    private static void DrawArc(Painter2D p, float cx, float cy,
                                 float radius, float fromDeg, float toDeg,
                                 Color color)
    {
        p.strokeColor = color;
        p.lineWidth   = StrokeWidth;
        p.lineCap     = LineCap.Round;

        int steps = Mathf.Max(2, Mathf.RoundToInt(Segments * Mathf.Abs(toDeg - fromDeg) / 360f));
        float step = (toDeg - fromDeg) / steps;

        p.BeginPath();
        for (int i = 0; i <= steps; i++)
        {
            float angleDeg = fromDeg + step * i;
            float rad      = Mathf.Deg2Rad * angleDeg;
            float x        = cx + Mathf.Cos(rad) * radius;
            float y        = cy + Mathf.Sin(rad) * radius;

            if (i == 0) p.MoveTo(new Vector2(x, y));
            else        p.LineTo(new Vector2(x, y));
        }
        p.Stroke();
    }
}
