using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Glowbo;

internal sealed class ScreensaverForm : Form
{
    private const int MoveThreshold = 8;
    private static readonly TimeSpan InputGrace = TimeSpan.FromMilliseconds(400);

    private readonly GlowboSettings settings = GlowboSettings.Load();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    private int frame;
    private Point startMouse;
    private DateTime shownAt;
    private bool closing;
    private readonly List<ScreenFace> faces = new();

    public ScreensaverForm()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; KeyPreview = true;
        StartPosition = FormStartPosition.Manual; Bounds = Screen.PrimaryScreen!.Bounds; BackColor = settings.Background;
        var screens = settings.MultiMonitor ? Screen.AllScreens : new[] { Screen.PrimaryScreen! };
        foreach (var screen in screens) { var f = new ScreenFace(settings, screen.Bounds); f.InputReceived += (_, _) => OnUserInput(); faces.Add(f); }
        KeyDown += (_, _) => OnUserInput(); MouseDown += (_, _) => OnUserInput(); MouseWheel += (_, _) => OnUserInput();
        MouseMove += (_, _) => CheckMouseMoved();
        timer.Tick += (_, _) => {
            CheckMouseMoved(); // polling fallback: exits even if no window receives mouse messages
            frame++;
            string expr = settings.ExpressionAt(frame, timer.Interval);
            foreach (var f in faces) { f.Expression = expr; f.Frame = frame; f.Invalidate(); }
        };
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        startMouse = Cursor.Position; shownAt = DateTime.UtcNow;
        foreach (var f in faces) f.Show(this);
        faces.FirstOrDefault()?.Activate();
        System.Windows.Forms.Cursor.Hide();
        timer.Start();
    }
    private void CheckMouseMoved()
    {
        var p = Cursor.Position;
        if (Math.Abs(p.X - startMouse.X) > MoveThreshold || Math.Abs(p.Y - startMouse.Y) > MoveThreshold) OnUserInput();
    }
    private void OnUserInput()
    {
        if (closing || DateTime.UtcNow - shownAt < InputGrace) return;
        closing = true;
        BeginInvoke(Close); // defer so faces are not disposed inside their own event handlers
    }
    [DllImport("user32.dll")] private static extern bool LockWorkStation();

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        timer.Stop(); timer.Dispose(); foreach (var f in faces) f.Dispose(); System.Windows.Forms.Cursor.Show();
        // Windows locks by itself when it started us with the secure option; this also covers "Start screensaver now".
        if (settings.SecureLock) LockWorkStation();
        base.OnFormClosed(e);
    }
}

internal sealed class ScreenFace : Form
{
    private readonly GlowboSettings settings;
    public string Expression { get; set; } = "Happy";
    public int Frame { get; set; }
    public event EventHandler? InputReceived;
    public ScreenFace(GlowboSettings s, Rectangle bounds)
    {
        settings=s; StartPosition=FormStartPosition.Manual; Bounds=bounds; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=true; KeyPreview=true; BackColor=s.Background; DoubleBuffered=true;
        KeyDown += (_, _) => InputReceived?.Invoke(this, EventArgs.Empty);
        MouseDown += (_, _) => InputReceived?.Invoke(this, EventArgs.Empty);
        MouseWheel += (_, _) => InputReceived?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        FacePainter.Draw(e.Graphics, ClientRectangle, settings.Background, settings.FaceFor(Expression), Expression, Frame*.12f);
        if (settings.ShowClock) FacePainter.DrawClock(e.Graphics, ClientRectangle, settings.Background, settings.ClockFor(settings.FaceFor(Expression)));
    }
}

/// <summary>Draws the face inside the small monitor of the Windows Screen Saver dialog (/p &lt;hwnd&gt;).</summary>
internal sealed class PreviewForm : Form
{
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private const int GWL_STYLE = -16, WS_CHILD = 0x40000000;

    private readonly IntPtr host;
    private readonly GlowboSettings settings = GlowboSettings.Load();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 80 };
    private int frame;

    public PreviewForm(IntPtr host)
    {
        this.host = host;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
        timer.Tick += (_, _) => { frame++; Invalidate(); };
    }
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SetParent(Handle, host);
        SetWindowLong(Handle, GWL_STYLE, GetWindowLong(Handle, GWL_STYLE) | WS_CHILD);
        GetClientRect(host, out var r);
        Bounds = new Rectangle(0, 0, r.Right, r.Bottom);
        timer.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        string expr = settings.ExpressionAt(frame, timer.Interval);
        FacePainter.Draw(e.Graphics, ClientRectangle, settings.Background, settings.FaceFor(expr), expr, frame * .12f);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { timer.Dispose(); base.OnFormClosed(e); }
}
