using System.Drawing.Drawing2D;
using System.Drawing.Text;
namespace Glowbo;

internal static class FacePainter
{
    public static readonly string[] Names = { "Happy", "Angry", "Shy", "Blinking", "Sleepy", "Love", "Surprised", "Sad", "Wink", "Excited", "Confused", "Dizzy", "Silly", "Neutral", "Cool", "Starstruck", "Thinking", "Kiss", "Scared", "Proud" };

    // Face layout in design units (the design box is 190 x 150). Eyes span y -30..+14 and the
    // mouth sits around y +24..+34, so the whole face is vertically centred on cy.
    private const float EyeW = 34, EyeH = 44, EyeRadius = 13, EyeGap = 38, EyeCenterY = -8, MouthY = 27, LineWidth = 4.5f;

    public static void Draw(Graphics g, Rectangle area, Color bg, Color face, string expression, float pulse = 0, bool pixelGrid = true)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.Clear(bg);
        float u = Math.Min(area.Width / 190f, area.Height / 150f);
        float cx = area.Left + area.Width / 2f;
        float cy = area.Top + area.Height / 2f;

        // Idle life: gentle bob, slow glance left/right and a natural blink every few seconds.
        float bob = (float)Math.Sin(pulse) * 1.2f * u;
        float look = (float)(Math.Sin(pulse * .31) * .7 + Math.Sin(pulse * .13) * .3) * 3f * u;
        float open = Blink(pulse, expression == "Blinking" ? 2.4f : 6.5f, .45f);
        if (expression == "Shy" && pulse % 5 > 4.2f) open = .15f;

        float ey = cy + EyeCenterY * u + bob;
        float lx = cx - EyeGap * u + look, rx = cx + EyeGap * u + look;
        float my = cy + MouthY * u + bob;
        float w = EyeW * u, h = EyeH * u;

        var ink = new Ink(g, face, bg, u);
        switch (expression)
        {
            case "Happy":
                // Glowbo's look: square eyes with rounded corners, a wider smile and rosy cheeks.
                Eyes(ink, lx, rx, ey, w * 1.1f, h * .88f, open, EyeRadius * .8f);
                ink.Stroke(Arc(cx - 12 * u, my - 8 * u, 24 * u, 13 * u, 25, 130));
                ink.Fill(Ellipse(lx - 7 * u, my - 7 * u, 9 * u, 6 * u), dim: true);
                ink.Fill(Ellipse(rx - 2 * u, my - 7 * u, 9 * u, 6 * u), dim: true);
                break;
            case "Cool":
                foreach (float x in new[] { lx, rx })
                {
                    ink.Fill(RoundedPoly(new PointF[] { new(x - w * .62f, ey - h * .3f), new(x + w * .62f, ey - h * .3f), new(x + w * .5f, ey + h * .22f), new(x - w * .45f, ey + h * .22f) }, 8 * u));
                    ink.CutStroke(Line(x - w * .3f, ey + h * .02f, x - w * .05f, ey - h * .18f), 2.2f * u);
                }
                ink.Stroke(Line(lx + w * .6f, ey - h * .22f, rx - w * .6f, ey - h * .22f), widthScale: .8f);
                ink.Stroke(Arc(cx - 4 * u, my - 7 * u, 20 * u, 11 * u, 20, 120));
                break;
            case "Starstruck":
                float twinkle = 1 + (float)Math.Sin(pulse * 3) * .08f, tilt = (float)Math.Sin(pulse) * .2f;
                ink.Fill(Star(lx, ey, w * .68f * twinkle, 5, .5f, tilt));
                ink.Fill(Star(rx, ey, w * .68f * twinkle, 5, .5f, -tilt));
                ink.Fill(HalfDisc(cx - 11 * u, my - 6 * u, 22 * u, 14 * u));
                break;
            case "Thinking":
                Eyes(ink, lx + 5 * u, rx + 5 * u, ey - 5 * u, w * .9f, h * .72f, open);
                ink.Stroke(Line(cx - 7 * u, my + 3 * u, cx + 8 * u, my));
                int dots = (int)(pulse * 1.2f % 4);
                for (int i = 0; i < dots; i++) ink.Fill(Ellipse(rx + (16 + i * 9) * u, ey - (30 + i * 5) * u, (3 + i) * u, (3 + i) * u));
                break;
            case "Kiss":
                ink.Stroke(Arc(lx - w / 2, ey - 6 * u, w, 16 * u, 200, 140));
                ink.Stroke(Arc(rx - w / 2, ey - 6 * u, w, 16 * u, 200, 140));
                ink.Stroke(Arc(cx - 4 * u, my - 7 * u, 8 * u, 7 * u, 270, 180), widthScale: .8f);
                ink.Stroke(Arc(cx - 4 * u, my, 8 * u, 7 * u, 270, 180), widthScale: .8f);
                float life = pulse * .4f % 1f;
                ink.Fill(Heart(cx + (12 + life * 10) * u, my - (2 + life * 16) * u, 9 * u * (1 - life * .4f)), dim: true);
                break;
            case "Scared":
                float shake = (float)Math.Sin(pulse * 25) * 1.2f * u;
                Eyes(ink, lx + shake, rx + shake, ey, w * .72f, h * .68f, open);
                var zig = new GraphicsPath();
                zig.AddLines(Enumerable.Range(0, 7).Select(i => new PointF(cx - 12 * u + i * 4 * u + shake, my + (i % 2 == 0 ? 3 : -1) * u)).ToArray());
                ink.Stroke(zig, widthScale: .75f);
                ink.Fill(Tear(rx + w * .62f, ey - h * .62f + pulse * 3 % 14 * u, 4 * u), dim: true);
                break;
            case "Proud":
                ink.Stroke(Arc(lx - w / 2, ey - 6 * u, w, 12 * u, 20, 140));
                ink.Stroke(Arc(rx - w / 2, ey - 6 * u, w, 12 * u, 20, 140));
                ink.Stroke(Arc(cx - 14 * u, my - 9 * u, 28 * u, 15 * u, 15, 150));
                float glint1 = (float)Math.Abs(Math.Sin(pulse * 1.7)), glint2 = (float)Math.Abs(Math.Sin(pulse * 1.7 + 1.3));
                ink.Fill(Star(lx - 28 * u, ey - 22 * u, 9 * u * glint1 + u, 4, .3f, 0));
                ink.Fill(Star(rx + 28 * u, ey - 26 * u, 7 * u * glint2 + u, 4, .3f, 0));
                break;
            case "Angry":
                ink.Fill(RoundedPoly(EyeQuad(lx, ey + 2 * u, w, h * .85f * open, 0, 13 * u * open), EyeRadius * u * .7f));
                ink.Fill(RoundedPoly(EyeQuad(rx, ey + 2 * u, w, h * .85f * open, 13 * u * open, 0), EyeRadius * u * .7f));
                ink.Stroke(Arc(cx - 10 * u, my + 2 * u, 20 * u, 10 * u, 200, 140));
                break;
            case "Sad":
                ink.Fill(RoundedPoly(EyeQuad(lx, ey + 3 * u, w, h * .85f * open, 11 * u * open, 0), EyeRadius * u * .7f));
                ink.Fill(RoundedPoly(EyeQuad(rx, ey + 3 * u, w, h * .85f * open, 0, 11 * u * open), EyeRadius * u * .7f));
                ink.Stroke(Arc(cx - 10 * u, my + 1 * u, 20 * u, 12 * u, 200, 140));
                float tear = (pulse * 6 % 30) * u;
                if (tear < 24 * u) ink.Fill(Tear(lx + w * .3f, ey + h * .45f + tear, 5 * u), dim: true);
                break;
            case "Shy":
                Eyes(ink, lx - look * .5f, rx - look * .5f, ey + 4 * u, w * .9f, h * .8f, open);
                ink.Fill(Ellipse(lx - 13 * u, my - 6 * u, 22 * u, 9 * u), dim: true);
                ink.Fill(Ellipse(rx - 9 * u, my - 6 * u, 22 * u, 9 * u), dim: true);
                ink.Stroke(Wave(cx - 8 * u, my + 2 * u, 16 * u, 2.2f * u, 2));
                break;
            case "Blinking": case "Neutral":
                Eyes(ink, lx, rx, ey, w, h, open);
                ink.Stroke(Line(cx - 8 * u, my + 2 * u, cx + 8 * u, my + 2 * u));
                break;
            case "Sleepy":
                ink.Stroke(Arc(lx - w / 2, ey - 2 * u, w, 10 * u, 20, 140));
                ink.Stroke(Arc(rx - w / 2, ey - 2 * u, w, 10 * u, 20, 140));
                float breathe = 1 + (float)Math.Sin(pulse * .6) * .25f;
                ink.Stroke(Ellipse(cx - 4 * u * breathe, my - 1 * u, 8 * u * breathe, 8 * u * breathe));
                SleepyZ(g, face, u, cx + 55 * u, ey - 22 * u, pulse);
                break;
            case "Love":
                float beat = 1 + (float)Math.Max(0, Math.Sin(pulse * 2.2)) * .1f;
                ink.Fill(Heart(lx, ey, 40 * u * beat));
                ink.Fill(Heart(rx, ey, 40 * u * beat));
                ink.Stroke(Arc(cx - 9 * u, my - 5 * u, 18 * u, 11 * u, 20, 140));
                break;
            case "Surprised":
                Eyes(ink, lx, rx, ey - 3 * u, w * 1.08f, h * 1.12f, Math.Max(open, .6f));
                ink.Stroke(Ellipse(cx - 6 * u, my - 2 * u, 12 * u, 14 * u));
                break;
            case "Wink":
                ink.Fill(RoundedRect(lx, ey, w, h * open, EyeRadius * u));
                ink.Stroke(Arc(rx - w / 2, ey - 6 * u, w, 16 * u, 200, 140));
                ink.Stroke(Arc(cx - 9 * u, my - 6 * u, 18 * u, 11 * u, 20, 140));
                break;
            case "Excited":
                float hop = (float)Math.Abs(Math.Sin(pulse * 2)) * -3 * u;
                ink.Stroke(Arc(lx - w / 2, ey - 4 * u + hop, w, 22 * u, 200, 140));
                ink.Stroke(Arc(rx - w / 2, ey - 4 * u + hop, w, 22 * u, 200, 140));
                ink.Fill(HalfDisc(cx - 13 * u, my - 8 * u + hop, 26 * u, 22 * u));
                break;
            case "Confused":
                ink.Fill(RoundedRect(lx, ey, w, h * open, EyeRadius * u));
                ink.Fill(RoundedRect(rx, ey + 4 * u, w * .8f, h * .6f * open, EyeRadius * u * .8f));
                ink.Stroke(Wave(cx - 10 * u, my + 2 * u, 20 * u, 2.5f * u, 2));
                break;
            case "Dizzy":
                ink.Stroke(Spiral(lx, ey, w * .55f, pulse * 2.5f), widthScale: .8f);
                ink.Stroke(Spiral(rx, ey, w * .55f, pulse * 2.5f), widthScale: .8f);
                ink.Stroke(Wave(cx - 10 * u, my + 2 * u, 20 * u, 2.5f * u, 3));
                break;
            case "Silly":
                Eyes(ink, lx, rx, ey, w, h, open);
                ink.Stroke(Arc(cx - 13 * u, my - 7 * u, 26 * u, 12 * u, 10, 160));
                ink.Fill(HalfDisc(cx + 1 * u, my + 4 * u, 9 * u, 9 * u));
                break;
            default:
                Eyes(ink, lx, rx, ey, w, h, open);
                ink.Stroke(Line(cx - 8 * u, my + 2 * u, cx + 8 * u, my + 2 * u));
                break;
        }

        if (pixelGrid) PixelGrid(g, bg, u, new RectangleF(cx - 95 * u, cy - 75 * u, 190 * u, 150 * u));
    }

    /// <summary>1 = open, dips to 0 and back during a blink.</summary>
    private static float Blink(float t, float period, float length)
    {
        float phase = t % period;
        if (phase > length) return 1;
        return Math.Max(.08f, Math.Abs(phase / length * 2 - 1));
    }

    private static void Eyes(Ink ink, float lx, float rx, float ey, float w, float h, float open, float radius = EyeRadius)
    {
        float r = Math.Min(radius * ink.U, Math.Min(w, h * open) / 2);
        ink.Fill(RoundedRect(lx, ey, w, h * open, r));
        ink.Fill(RoundedRect(rx, ey, w, h * open, r));
    }

    /// <summary>Clock and date in the bottom-right corner (screensaver option).</summary>
    public static void DrawClock(Graphics g, Rectangle area, Color bg, Color face)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float timePx = Math.Clamp(area.Height * .055f, 14, 84), datePx = timePx * .46f, margin = Math.Max(10, area.Height * .035f);
        var ink = new Ink(g, face, bg, timePx / 60f);
        using var family = new FontFamily("Segoe UI");
        var now = DateTime.Now;
        using var time = new GraphicsPath();
        time.AddString(now.ToString("t"), family, (int)FontStyle.Regular, timePx, PointF.Empty, StringFormat.GenericTypographic);
        using var date = new GraphicsPath();
        date.AddString(now.ToString("D"), family, (int)FontStyle.Regular, datePx, PointF.Empty, StringFormat.GenericTypographic);
        var tb = time.GetBounds(); var db = date.GetBounds();
        float right = area.Right - margin, bottom = area.Bottom - margin;
        using (var m = new Matrix()) { m.Translate(right - db.Right, bottom - db.Bottom); date.Transform(m); }
        using (var m = new Matrix()) { m.Translate(right - tb.Right, bottom - db.Height - datePx * .5f - tb.Bottom); time.Transform(m); }
        ink.Fill((GraphicsPath)time.Clone());
        ink.Fill((GraphicsPath)date.Clone(), dim: true);
    }

    // ---------- Shapes (all centred on the given point unless noted) ----------

    private static GraphicsPath RoundedRect(float cx, float cy, float w, float h, float r)
    {
        h = Math.Max(h, 2.5f);
        return RoundedPoly(new PointF[] { new(cx - w / 2, cy - h / 2), new(cx + w / 2, cy - h / 2), new(cx + w / 2, cy + h / 2), new(cx - w / 2, cy + h / 2) }, Math.Min(r, h / 2));
    }

    /// <summary>Eye rectangle with the top-left / top-right corners pushed down (for angry and sad brows).</summary>
    private static PointF[] EyeQuad(float cx, float cy, float w, float h, float dropLeft, float dropRight)
    {
        h = Math.Max(h, 2.5f);
        float top = cy - h / 2, bottom = cy + h / 2;
        return new PointF[] { new(cx - w / 2, Math.Min(top + dropLeft, bottom - 2)), new(cx + w / 2, Math.Min(top + dropRight, bottom - 2)), new(cx + w / 2, bottom), new(cx - w / 2, bottom) };
    }

    /// <summary>Polygon whose corners are rounded with cubic curves.</summary>
    private static GraphicsPath RoundedPoly(PointF[] pts, float r)
    {
        var path = new GraphicsPath();
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            PointF prev = pts[(i + n - 1) % n], cur = pts[i], next = pts[(i + 1) % n];
            float d1 = Dist(cur, prev), d2 = Dist(cur, next);
            float rr = Math.Min(r, Math.Min(d1, d2) / 2);
            if (rr < .5f) { path.AddLine(cur, cur); continue; }
            var a = Lerp(cur, prev, rr / d1);
            var b = Lerp(cur, next, rr / d2);
            path.AddBezier(a, Lerp(a, cur, .55f), Lerp(b, cur, .55f), b);
        }
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Heart(float cx, float cy, float s)
    {
        float top = cy - s * .45f, hw = s * .5f, h = s * .9f;
        var p = new GraphicsPath();
        p.AddBezier(cx, top + h * .25f, cx, top, cx - hw, top - h * .02f, cx - hw, top + h * .3f);
        p.AddBezier(cx - hw, top + h * .3f, cx - hw, top + h * .6f, cx - hw * .2f, top + h * .78f, cx, top + h);
        p.AddBezier(cx, top + h, cx + hw * .2f, top + h * .78f, cx + hw, top + h * .6f, cx + hw, top + h * .3f);
        p.AddBezier(cx + hw, top + h * .3f, cx + hw, top - h * .02f, cx, top, cx, top + h * .25f);
        p.CloseFigure();
        return p;
    }

    /// <summary>Star with the given number of points; inner = inner radius as a fraction of the outer one.</summary>
    private static GraphicsPath Star(float cx, float cy, float radius, int points, float inner, float rotation)
    {
        var pts = new PointF[points * 2];
        for (int i = 0; i < pts.Length; i++)
        {
            double a = -Math.PI / 2 + rotation + i * Math.PI / points;
            float r = i % 2 == 0 ? radius : radius * inner;
            pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
        }
        return RoundedPoly(pts, radius * .12f);
    }

    private static GraphicsPath Tear(float cx, float top, float r)
    {
        var p = new GraphicsPath();
        p.AddBezier(cx, top, cx + r * .2f, top + r, cx + r, top + r * 1.3f, cx + r, top + r * 2);
        p.AddArc(cx - r, top + r, r * 2, r * 2, 0, 180);
        p.AddBezier(cx - r, top + r * 2, cx - r, top + r * 1.3f, cx - r * .2f, top + r, cx, top);
        p.CloseFigure();
        return p;
    }

    private static GraphicsPath Spiral(float cx, float cy, float radius, float spin)
    {
        var pts = new PointF[48];
        for (int i = 0; i < pts.Length; i++)
        {
            float t = i / (float)(pts.Length - 1);
            double a = t * Math.PI * 4 + spin;
            pts[i] = new PointF(cx + (float)Math.Cos(a) * radius * t, cy + (float)Math.Sin(a) * radius * t);
        }
        var p = new GraphicsPath(); p.AddCurve(pts); return p;
    }

    private static GraphicsPath Wave(float x, float y, float w, float amp, int humps)
    {
        var pts = new PointF[humps * 2 + 1];
        for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(x + w * i / (pts.Length - 1), y + (i % 2 == 0 ? 0 : (i % 4 == 1 ? -amp : amp)));
        var p = new GraphicsPath(); p.AddCurve(pts, .5f); return p;
    }

    private static GraphicsPath HalfDisc(float x, float y, float w, float h)
    {
        var p = new GraphicsPath(); p.AddArc(x, y - h, w, h * 2, 0, 180); p.CloseFigure(); return p;
    }
    private static GraphicsPath Arc(float x, float y, float w, float h, float start, float sweep)
    {
        var p = new GraphicsPath(); p.AddArc(x, y, w, h, start, sweep); return p;
    }
    private static GraphicsPath Ellipse(float x, float y, float w, float h)
    {
        var p = new GraphicsPath(); p.AddEllipse(x, y, w, h); return p;
    }
    private static GraphicsPath Line(float x1, float y1, float x2, float y2)
    {
        var p = new GraphicsPath(); p.AddLine(x1, y1, x2, y2); return p;
    }

    private static void SleepyZ(Graphics g, Color face, float u, float x, float y, float t)
    {
        for (int i = 0; i < 3; i++)
        {
            float life = (t * .5f + i / 3f) % 1f;
            int alpha = (int)(255 * Math.Sin(life * Math.PI));
            using var f = new Font("Segoe UI", Math.Max(6, (9 + i * 4) * u), FontStyle.Bold, GraphicsUnit.Pixel);
            using var b = new SolidBrush(Color.FromArgb(alpha, face));
            g.DrawString("z", f, b, x + i * 8 * u + life * 6 * u, y - i * 9 * u - life * 10 * u);
        }
    }

    /// <summary>Faint dot-matrix lines over the face so it reads like a small OLED panel.</summary>
    private static void PixelGrid(Graphics g, Color bg, float u, RectangleF box)
    {
        float pitch = Math.Max(3, u * 1.6f);
        using var pen = new Pen(Color.FromArgb(38, bg), Math.Max(1, pitch * .14f));
        var old = g.SmoothingMode; g.SmoothingMode = SmoothingMode.None;
        for (float x = box.Left; x < box.Right; x += pitch) g.DrawLine(pen, x, box.Top, x, box.Bottom);
        for (float y = box.Top; y < box.Bottom; y += pitch) g.DrawLine(pen, box.Left, y, box.Right, y);
        g.SmoothingMode = old;
    }

    private static float Dist(PointF a, PointF b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>Draws shapes with a soft OLED-style bloom behind them.</summary>
    private readonly struct Ink
    {
        private static readonly (float width, int alpha)[] GlowLayers = { (9, 10), (6, 18), (3.5f, 32), (1.6f, 60) };
        private readonly Graphics g; private readonly Color c, bg;
        public float U { get; }
        public Ink(Graphics g, Color c, Color bg, float u) { this.g = g; this.c = c; this.bg = bg; U = u; }

        /// <summary>Paints a shape in the background color (highlights cut into a lit shape).</summary>
        public void Cut(GraphicsPath path)
        {
            using (path) { using var b = new SolidBrush(bg); g.FillPath(b, path); }
        }
        public void CutStroke(GraphicsPath path, float width)
        {
            using (path) { using var pen = RoundPen(bg, width); g.DrawPath(pen, path); }
        }

        public void Fill(GraphicsPath path, bool dim = false)
        {
            using (path)
            {
                Color col = dim ? Color.FromArgb(110, c) : c;
                Glow(path, 0, dim ? .5f : 1);
                using var b = new SolidBrush(col);
                g.FillPath(b, path);
            }
        }
        public void Stroke(GraphicsPath path, float widthScale = 1)
        {
            using (path)
            {
                float w = Math.Max(2, LineWidth * U * widthScale);
                Glow(path, w, 1);
                using var pen = RoundPen(c, w);
                g.DrawPath(pen, path);
            }
        }
        private void Glow(GraphicsPath path, float baseWidth, float strength)
        {
            foreach (var (width, alpha) in GlowLayers)
            {
                using var pen = RoundPen(Color.FromArgb((int)(alpha * strength), c), baseWidth + width * U);
                g.DrawPath(pen, path);
            }
        }
        private static Pen RoundPen(Color col, float w) => new(col, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
    }
}
