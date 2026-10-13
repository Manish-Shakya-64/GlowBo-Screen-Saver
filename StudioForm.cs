using System.Drawing;
using System.Windows.Forms;

namespace Glowbo;

internal sealed class StudioForm : Form
{
    private static readonly Color Accent = Color.MediumTurquoise;
    private static readonly Color Muted = Color.Gainsboro;

    private readonly GlowboSettings settings = GlowboSettings.Load();
    private readonly FacePreview preview = new() { Dock = DockStyle.Fill };
    private readonly CheckedListBox playlist = new() { Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false };
    private readonly ComboBox facePicker = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox autoChange = new() { Text = "Automatically rotate selected faces", AutoSize = true };
    private readonly NumericUpDown interval = new() { Minimum = 2, Maximum = 300 };
    private readonly NumericUpDown idle = new() { Minimum = 1, Maximum = 120 };
    private readonly CheckBox multi = new() { Text = "Use all monitors", AutoSize = true };
    private readonly CheckBox showClock = new() { Text = "Show date and time (bottom-right)", AutoSize = true };
    private Button clockColorBtn = null!;
    private readonly RadioButton modeSaver = new() { Text = "Screensaver only", AutoSize = true, FlatStyle = FlatStyle.Flat };
    private readonly RadioButton modeLock = new() { Text = "Glowbo secure lock (PIN needed on return)", AutoSize = true, FlatStyle = FlatStyle.Flat };
    private readonly Label colorScope = new() { AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly TableLayoutPanel body = new() { Dock = DockStyle.Fill };
    private readonly Panel previewHost = new() { Dock = DockStyle.Fill };
    private readonly Panel settingsHost = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly TableLayoutPanel form = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
    private Color faceColor, bgColor;
    private readonly Dictionary<string, Color> faceColors = new();
    private readonly System.Windows.Forms.Timer previewTimer = new() { Interval = 90 };
    private int frame;
    private bool? narrowLayout;

    public StudioForm()
    {
        Text = "Glowbo";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(22, 25, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        faceColor = settings.Face; bgColor = settings.Background;
        foreach (var name in settings.FaceColors.Keys) faceColors[name] = settings.FaceFor(name);

        // Fit the first window inside the screen's work area (laptops are often 1366x768 at 125-150% scaling).
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        Size = new Size(Math.Min(S(1080), (int)(work.Width * .92)), Math.Min(S(760), (int)(work.Height * .92)));
        MinimumSize = new Size(Math.Min(S(480), work.Width), Math.Min(S(460), work.Height));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(S(16), S(10), S(16), S(10)) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(body, 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);

        previewHost.Padding = new Padding(0, S(6), 0, S(6));
        previewHost.Controls.Add(preview);
        settingsHost.Padding = new Padding(S(4), 0, S(8), 0);
        BuildSettings();
        settingsHost.Controls.Add(form);
        body.Controls.Add(previewHost);
        body.Controls.Add(settingsHost);
        ApplyLayout();
        Resize += (_, _) => ApplyLayout();
        DpiChanged += (_, _) => { narrowLayout = null; ApplyLayout(); };

        preview.FaceColor = faceColor; preview.BackgroundColor = bgColor; preview.Expression = CurrentFace; preview.ShowClock = settings.ShowClock;
        // The preview only animates the selected face; it never switches faces by itself, so edits always
        // apply to the face shown in the dropdown. Rotation happens in the real screensaver.
        previewTimer.Tick += (_, _) => { frame++; preview.Frame = frame; preview.Invalidate(); };
        RefreshFaceColor();
        previewTimer.Start();
        TrackChanges();
        FormClosing += OnClosingWithUnsavedChanges;
        FormClosed += (_, _) => previewTimer.Dispose();
    }

    // ---------- Unsaved changes ----------

    private bool dirty, loading = true;
    private Button saveBtn = null!;

    /// <summary>All edits stay pending until "Save settings"; the title and Save button show when something is unsaved.</summary>
    private void MarkDirty()
    {
        if (loading || dirty) return;
        dirty = true;
        Text = "Glowbo • unsaved changes";
        saveBtn.Text = "Save settings •";
    }

    private void MarkClean()
    {
        dirty = false;
        Text = "Glowbo";
        saveBtn.Text = "Save settings";
    }

    private void TrackChanges()
    {
        autoChange.CheckedChanged += (_, _) => MarkDirty();
        interval.ValueChanged += (_, _) => MarkDirty();
        idle.ValueChanged += (_, _) => MarkDirty();
        multi.CheckedChanged += (_, _) => MarkDirty();
        showClock.CheckedChanged += (_, _) => MarkDirty();
        modeLock.CheckedChanged += (_, _) => MarkDirty();
        playlist.ItemCheck += (_, _) => MarkDirty();
        loading = false;
    }

    private void OnClosingWithUnsavedChanges(object? sender, FormClosingEventArgs e)
    {
        if (!dirty) return;
        var answer = MessageBox.Show("You have unsaved changes. Save them before closing?", "Glowbo", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Yes) SaveSettings(false);
        else if (answer == DialogResult.Cancel) e.Cancel = true;
    }

    /// <summary>Scales a 96-DPI pixel value to the current monitor.</summary>
    private int S(int value) => LogicalToDeviceUnits(value);

    // ---------- Layout ----------

    private Control BuildHeader()
    {
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, S(6)) };
        header.Controls.Add(new Label { Text = "GLOWBO", Font = new Font("Segoe UI", 18, FontStyle.Bold), ForeColor = Accent, AutoSize = true, Margin = new Padding(0) });
        header.Controls.Add(new Label { Text = "Cute face screensaver · preview · colors · behavior", ForeColor = Color.Silver, AutoSize = true, Margin = new Padding(S(2), 0, 0, 0) });
        return header;
    }

    /// <summary>Wide windows: preview left, settings right. Narrow windows: preview on top, settings below.</summary>
    private void ApplyLayout()
    {
        bool narrow = ClientSize.Width < S(860);
        if (narrow == narrowLayout) return;
        narrowLayout = narrow;
        body.SuspendLayout();
        body.ColumnStyles.Clear(); body.RowStyles.Clear();
        if (narrow)
        {
            body.ColumnCount = 1; body.RowCount = 2;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
            body.SetCellPosition(previewHost, new TableLayoutPanelCellPosition(0, 0));
            body.SetCellPosition(settingsHost, new TableLayoutPanelCellPosition(0, 1));
            previewHost.Padding = new Padding(0, S(4), 0, S(8));
        }
        else
        {
            body.ColumnCount = 2; body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(430)));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.SetCellPosition(previewHost, new TableLayoutPanelCellPosition(0, 0));
            body.SetCellPosition(settingsHost, new TableLayoutPanelCellPosition(1, 0));
            previewHost.Padding = new Padding(0, S(6), S(16), S(6));
        }
        body.ResumeLayout(true);
    }

    private void BuildSettings()
    {
        clockColorBtn = MakeButton("Clock color…", null);
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Expression
        AddSection("Expression");
        facePicker.Items.AddRange(FacePainter.Names); facePicker.SelectedItem = settings.SelectedFace; if (facePicker.SelectedIndex < 0) facePicker.SelectedIndex = 0;
        facePicker.SelectedIndexChanged += (_, _) => { preview.Expression = CurrentFace; RefreshFaceColor(); };
        AddWide(facePicker);
        int count = facePicker.Items.Count;
        var prev = MakeButton("‹ Previous", (_, _) => facePicker.SelectedIndex = (facePicker.SelectedIndex + count - 1) % count);
        var next = MakeButton("Next ›", (_, _) => facePicker.SelectedIndex = (facePicker.SelectedIndex + 1) % count);
        var play = MakeButton("Pause", null);
        play.Click += (_, _) => { previewTimer.Enabled = !previewTimer.Enabled; play.Text = previewTimer.Enabled ? "Pause" : "Play"; };
        AddWide(Row(prev, next, play));

        // Colors
        AddSection("Colors", colorScope);
        AddWide(Pair(MakeButton("Face color…", (_, _) => ChooseColor(true)), MakeButton("Background…", (_, _) => ChooseColor(false))));
        // One-click OLED face colors (the color dialog keeps white's full brightness, so hues are hard to pick there).
        var swatchRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, S(2), 0, S(2)) };
        foreach (string hex in new[] { "#FFFFFF", "#7DF9FF", "#3DFF9A", "#FFE14D", "#FFA53D", "#FF6FB5", "#B98CFF", "#FF5A5A" })
        {
            Color c = ColorTranslator.FromHtml(hex);
            var sw = new Button { Size = new Size(S(38), S(26)), BackColor = c, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Margin = new Padding(0, 0, S(7), S(4)) };
            sw.FlatAppearance.BorderColor = Color.FromArgb(70, 75, 85);
            sw.Click += (_, _) => SetCurrentFaceColor(c);
            swatchRow.Controls.Add(sw);
        }
        AddWide(swatchRow);
        AddWide(Pair(
            MakeButton("Use color for all faces", (_, _) => { faceColor = ColorFor(CurrentFace); faceColors.Clear(); RefreshFaceColor(); MarkDirty(); }),
            MakeButton("Reset this face", (_, _) => { faceColors.Remove(CurrentFace); RefreshFaceColor(); MarkDirty(); })));

        // Rotation
        AddSection("Rotation");
        autoChange.Checked = settings.AutoChange;
        AddWide(autoChange);
        interval.Value = Math.Clamp(settings.ChangeSeconds, 2, 300);
        AddField("Change face every (seconds)", interval);
        AddWide(MakeLabel("Include these faces in automatic rotation"));
        foreach (string name in FacePainter.Names) playlist.Items.Add(name, settings.Playlist.Contains(name));
        playlist.Height = S(150);
        AddWide(playlist, SizeType.Absolute, S(156));

        // Screensaver
        AddSection("Screensaver");
        idle.Value = Math.Clamp(settings.IdleMinutes, 1, 120);
        AddField("Start after idle for (minutes)", idle);
        multi.Checked = settings.MultiMonitor;
        AddWide(multi);
        showClock.Checked = settings.ShowClock;
        showClock.CheckedChanged += (_, _) => { preview.ShowClock = showClock.Checked; preview.Invalidate(); };
        AddWide(showClock);
        AddWide(Pair(clockColorBtn, MakeButton("Match face color", (_, _) => SetClockColor(null))));
        clockColorBtn.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = preview.ClockColor ?? preview.FaceColor, FullOpen = true };
            if (dlg.ShowDialog() == DialogResult.OK) SetClockColor(dlg.Color);
        };
        SetClockColor(string.IsNullOrEmpty(settings.ClockColor) ? null : settings.ClockFor(faceColor));
        modeSaver.Checked = !settings.SecureLock; modeLock.Checked = settings.SecureLock;
        var tips = new ToolTip();
        tips.SetToolTip(modeSaver, "Glowbo plays when idle. Moving the mouse returns straight to your desktop.");
        tips.SetToolTip(modeLock, "Glowbo plays when idle. When you come back, the PC is locked and asks for your PIN/password.");
        modeLock.MaximumSize = new Size(S(390), 0);
        AddWide(modeSaver);
        AddWide(modeLock);
    }

    private Control BuildFooter()
    {
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, S(8), 0, 0) };
        var save = saveBtn = MakeButton("Save settings", (_, _) => SaveSettings(), fill: false);
        save.BackColor = Color.FromArgb(0, 120, 105); save.FlatStyle = FlatStyle.Flat; save.FlatAppearance.BorderColor = Color.FromArgb(0, 150, 130);
        footer.Controls.Add(save);
        footer.Controls.Add(MakeButton("Start screensaver now", (_, _) => { SaveSettings(false); Hide(); using var screen = new ScreensaverForm(); screen.ShowDialog(); Show(); }, fill: false));
        footer.Controls.Add(MakeButton("Install as Windows screensaver", (_, _) => InstallScreensaver(), fill: false));
        footer.Controls.Add(MakeButton("Windows screen saver settings…", (_, _) => ScreensaverInstaller.OpenWindowsSettings(), fill: false));
        footer.Controls.Add(MakeButton("Remove from Windows…", (_, _) => RemoveFromWindows(), fill: false));
        if (AppInstaller.CanInstall)
            footer.Controls.Add(MakeButton(AppInstaller.IsInstalled ? "Update installed app" : "Install app on this PC", (_, _) =>
            {
                if (dirty) SaveSettings(false);
                if (AppInstaller.InstallAndLaunch()) Close();
            }, fill: false));
        return footer;
    }

    private void AddSection(string title, Control? right = null)
    {
        var label = new Label { Text = title.ToUpperInvariant(), ForeColor = Accent, Font = new Font(Font, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, form.RowCount == 0 ? S(4) : S(16), 0, S(4)) };
        int row = NewRow(SizeType.AutoSize);
        if (right is null) { form.Controls.Add(label, 0, row); form.SetColumnSpan(label, 2); }
        else { form.Controls.Add(label, 0, row); right.Margin = label.Margin; form.Controls.Add(right, 1, row); }
    }

    /// <summary>Label on the left (wraps if needed), input on the right.</summary>
    private void AddField(string text, NumericUpDown input)
    {
        input.Width = S(80); input.Anchor = AnchorStyles.Right; input.Margin = new Padding(S(8), S(3), 0, S(3));
        int row = NewRow(SizeType.AutoSize);
        form.Controls.Add(MakeLabel(text), 0, row);
        form.Controls.Add(input, 1, row);
    }

    private void AddWide(Control c, SizeType sizing = SizeType.AutoSize, float height = 0)
    {
        if (c.Dock == DockStyle.None) c.Anchor = AnchorStyles.Left;
        if (c.Margin == DefaultMargin) c.Margin = new Padding(0, S(3), 0, S(3));
        int row = NewRow(sizing, height);
        form.Controls.Add(c, 0, row);
        form.SetColumnSpan(c, 2);
    }

    private int NewRow(SizeType sizing, float height = 0)
    {
        form.RowStyles.Add(sizing == SizeType.AutoSize ? new RowStyle(SizeType.AutoSize) : new RowStyle(sizing, height));
        return form.RowCount++;
    }

    private Label MakeLabel(string text) => new() { Text = text, ForeColor = Muted, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, S(3), 0, S(3)) };

    private Button MakeButton(string text, EventHandler? click, bool fill = true)
    {
        var b = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(S(120), S(34)), Padding = new Padding(S(8), 0, S(8), 0), Margin = new Padding(0, 0, S(8), S(6)), BackColor = Color.FromArgb(34, 38, 46), FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderColor = Color.FromArgb(90, 96, 108);
        if (fill) { b.AutoSize = false; b.Dock = DockStyle.Fill; b.Margin = new Padding(0, 0, S(6), 0); }
        if (click != null) b.Click += click;
        return b;
    }

    /// <summary>Two equal-width buttons side by side.</summary>
    private TableLayoutPanel Pair(Control left, Control right) => Row(left, right);

    /// <summary>Equal-width buttons side by side.</summary>
    private TableLayoutPanel Row(params Control[] items)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = items.Length, RowCount = 1, Margin = new Padding(0, S(3), 0, S(3)) };
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, S(36)));
        for (int i = 0; i < items.Length; i++)
        {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / items.Length));
            items[i].Margin = new Padding(i == 0 ? 0 : S(3), 0, i == items.Length - 1 ? 0 : S(3), 0);
            items[i].MinimumSize = new Size(S(60), items[i].MinimumSize.Height);
            t.Controls.Add(items[i], i, 0);
        }
        return t;
    }

    // ---------- Behaviour ----------

    private string CurrentFace => facePicker.SelectedItem?.ToString() ?? "Happy";
    private Color ColorFor(string face) => faceColors.TryGetValue(face, out var c) ? c : faceColor;

    /// <summary>Face color changes apply to the expression currently shown in the preview.</summary>
    private void SetCurrentFaceColor(Color c) { faceColors[CurrentFace] = c; RefreshFaceColor(); MarkDirty(); }

    /// <summary>null = clock follows the face color.</summary>
    private void SetClockColor(Color? c)
    {
        preview.ClockColor = c;
        MarkDirty();
        clockColorBtn.Text = c is null ? "Clock color… (face)" : "Clock color… ●";
        clockColorBtn.ForeColor = c ?? Color.White;
        preview.Invalidate();
    }

    private void RefreshFaceColor()
    {
        bool custom = faceColors.ContainsKey(CurrentFace);
        colorScope.Text = custom ? "● custom color" : "○ default color";
        colorScope.ForeColor = custom ? ColorFor(CurrentFace) : Color.Gray;
        preview.FaceColor = ColorFor(CurrentFace);
        preview.Invalidate();
    }

    private void ChooseColor(bool face)
    {
        using var dlg = new ColorDialog { Color = face ? ColorFor(CurrentFace) : bgColor, FullOpen = true };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        if (face) SetCurrentFaceColor(dlg.Color);
        else { bgColor = dlg.Color; preview.BackgroundColor = bgColor; preview.Invalidate(); MarkDirty(); }
    }

    /// <summary>Clean-up: unsets Glowbo as the screen saver, deletes its copy and optionally the saved settings.</summary>
    private void RemoveFromWindows()
    {
        if (MessageBox.Show("Remove the Glowbo screensaver from Windows?\n\nWindows will stop using Glowbo as your screen saver and its installed copy will be deleted.",
                "Glowbo", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        bool deleteSettings = MessageBox.Show("Also delete your saved faces, colors and settings?",
                "Glowbo", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        try
        {
            ScreensaverInstaller.Uninstall(deleteSettings);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not finish the clean-up:\n" + ex.Message, "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (deleteSettings) MarkClean();
        bool listedInApps = ScreensaverInstaller.CurrentPackageFamily() != null || AppInstaller.IsInstalled;
        string next = listedInApps
            ? "To uninstall the app itself, open Settings → Apps and uninstall Glowbo. Open it now?"
            : "To finish, close Glowbo and delete Glowbo.exe. Close Glowbo now?";
        if (MessageBox.Show("The Glowbo screensaver was removed from Windows.\n\n" + next, "Glowbo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
        if (listedInApps) ScreensaverInstaller.OpenInstalledApps();
        else Close();
    }

    private void InstallScreensaver()
    {
        SaveSettings(false);
        try
        {
            string scr = ScreensaverInstaller.Install((int)idle.Value);
            MessageBox.Show($"Glowbo is now your Windows screen saver.\n\nIt starts after {idle.Value} minute(s) of inactivity.\nMode: {(settings.SecureLock ? "Glowbo secure lock (PIN/password needed when you return)" : "Screensaver only")}\nInstalled to: {scr}\n\nYou can change the wait time or preview it in Windows screen saver settings.", "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not install the screen saver:\n" + ex.Message, "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveSettings(bool showMessage = true)
    {
        settings.FaceColor = ColorTranslator.ToHtml(faceColor);
        settings.BackgroundColor = ColorTranslator.ToHtml(bgColor);
        settings.FaceColors = faceColors.ToDictionary(kv => kv.Key, kv => ColorTranslator.ToHtml(kv.Value));
        settings.SelectedFace = facePicker.SelectedItem?.ToString() ?? "Happy";
        settings.AutoChange = autoChange.Checked;
        settings.ChangeSeconds = (int)interval.Value;
        settings.IdleMinutes = (int)idle.Value;
        settings.MultiMonitor = multi.Checked;
        settings.ShowClock = showClock.Checked;
        settings.ClockColor = preview.ClockColor is Color clock ? ColorTranslator.ToHtml(clock) : "";
        settings.SecureLock = modeLock.Checked;
        ScreensaverInstaller.ApplySecureLock(settings.SecureLock);
        settings.Playlist = playlist.CheckedItems.Cast<string>().ToList();
        if (settings.Playlist.Count == 0) settings.Playlist.Add(settings.SelectedFace);
        settings.Save();
        MarkClean();
        if (showMessage) MessageBox.Show("Settings saved.", "Glowbo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

internal sealed class FacePreview : Control
{
    public Color FaceColor { get; set; } = Color.White;
    public Color BackgroundColor { get; set; } = Color.Black;
    public string Expression { get; set; } = "Happy";
    public int Frame { get; set; }
    public bool ShowClock { get; set; }
    /// <summary>Clock color; null follows the face color.</summary>
    public Color? ClockColor { get; set; }
    public FacePreview() { DoubleBuffered = true; ResizeRedraw = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        FacePainter.Draw(e.Graphics, ClientRectangle, BackgroundColor, FaceColor, Expression, Frame * .12f);
        if (ShowClock) FacePainter.DrawClock(e.Graphics, ClientRectangle, BackgroundColor, ClockColor ?? FaceColor);
        using var p = new Pen(Color.FromArgb(60, 255, 255, 255));
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}
