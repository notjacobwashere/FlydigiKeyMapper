using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace FlydigiKeyMapper;

sealed record Palette(Color Background, Color Sidebar, Color Surface, Color Field, Color Line,
    Color Text, Color Muted, Color Highlight, Color HighlightText)
{
    public static Palette Create(Appearance appearance, Accent accent)
    {
        string[] colors = appearance switch
        {
            Appearance.Light => ["#EDF1F5", "#E3EAF1", "#FFFFFF", "#EBF0F5", "#CDD7E3", "#172437", "#435773"],
            Appearance.Slate => ["#202A39", "#18212E", "#283548", "#324157", "#465870", "#F4F7FE", "#BBC9DE"],
            _ => ["#0D131D", "#111925", "#151F2D", "#1C293A", "#283A50", "#F2F6FD", "#A5B6CC"]
        };
        string highlight = (appearance == Appearance.Light, accent) switch
        {
            (true, Accent.Blue) => "#2157AA",
            (true, Accent.Violet) => "#654DB3",
            (true, Accent.Amber) => "#855310",
            (true, _) => "#146348",
            (_, Accent.Blue) => "#86BEFF",
            (_, Accent.Violet) => "#C0ACFF",
            (_, Accent.Amber) => "#FFCF84",
            _ => "#74E6BD"
        };
        var c = colors.Select(ColorTranslator.FromHtml).ToArray();
        return new(c[0], c[1], c[2], c[3], c[4], c[5], c[6],
            ColorTranslator.FromHtml(highlight), appearance == Appearance.Light ? Color.White : Color.FromArgb(13, 24, 31));
    }

    public static Color Mix(Color color, Color overlay, double amount) => Color.FromArgb(
        (int)(color.R + (overlay.R - color.R) * amount),
        (int)(color.G + (overlay.G - color.G) * amount),
        (int)(color.B + (overlay.B - color.B) * amount));
}

enum Tone { Muted, Accent, Sidebar, Field }
enum ButtonKind { Standard, Primary, Navigation, Toggle, Quiet }

static class Ui
{
    public static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Label Text(string text, float size = 10, bool bold = false, Tone? tone = null)
    {
        return new Label
        {
            Text = text, Dock = DockStyle.Fill, AutoEllipsis = true, Margin = Padding.Empty, Tag = tone,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    public static StudioButton Button(string text, EventHandler click, ButtonKind kind = ButtonKind.Standard)
    {
        var button = new StudioButton { Text = text, Kind = kind, Dock = DockStyle.Fill };
        button.Click += click;
        return button;
    }

    public static TableLayoutPanel Rows(params int[] heights)
    {
        var pane = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length, Margin = Padding.Empty
        };
        pane.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (int h in heights)
            pane.RowStyles.Add(h == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, h));
        return pane;
    }

    public static TableLayoutPanel Columns(params int[] widths)
    {
        var pane = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = widths.Length, RowCount = 1, Margin = Padding.Empty
        };
        foreach (int w in widths)
            pane.ColumnStyles.Add(w <= 0 ? new ColumnStyle(SizeType.Percent, -w) : new ColumnStyle(SizeType.Absolute, w));
        pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return pane;
    }

    public static void Theme(Control control, Palette palette, Color background)
    {
        var surface = control.Tag switch
        {
            Tone.Sidebar => palette.Sidebar,
            Tone.Field => palette.Field,
            _ => background
        };
        control.BackColor = surface;
        control.ForeColor = control.Tag switch
        {
            Tone.Muted => palette.Muted, Tone.Accent => palette.Highlight, _ => palette.Text
        };
        if (control is Surface card)
        {
            card.Palette = palette;
            surface = palette.Surface;
        }
        if (control is StudioButton button) button.Palette = palette;
        if (control is StudioCombo combo) combo.Palette = palette;
        if (control is StudioCheckBox checkBox) checkBox.Palette = palette;
        if (control is ControllerDiagram diagram) diagram.Palette = palette;
        if (control is TextBoxBase or ListBox)
        {
            control.BackColor = palette.Field;
            control.ForeColor = palette.Text;
        }
        foreach (Control child in control.Controls) Theme(child, palette, surface);
        control.Invalidate();
    }

    public static void Glyph(Graphics g, string kind, RectangleF r, Color color)
    {
        var s = g.Save();
        g.TranslateTransform(r.X, r.Y);
        g.ScaleTransform(r.Width / 24, r.Height / 24);
        using var p = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        switch (kind)
        {
            case "mapping":
                using (var keys = Rounded(new RectangleF(2, 5, 20, 14), 3)) g.DrawPath(p, keys);
                g.DrawLine(p, 8, 14, 16, 14);
                foreach (int x in new[] { 6, 10, 14, 18 }) g.DrawLine(p, x, 10, x + .4f, 10);
                break;
            case "apps":
                using (var window = Rounded(new RectangleF(3, 4, 18, 16), 3)) g.DrawPath(p, window);
                g.DrawLine(p, 4, 9, 20, 9);
                g.DrawLine(p, 7, 6.5f, 7.5f, 6.5f);
                g.DrawLines(p, [new PointF(9, 15), new PointF(11, 17), new PointF(15, 13)]);
                break;
            case "settings":
                g.DrawLine(p, 3, 7, 21, 7); g.DrawLine(p, 3, 17, 21, 17);
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 7, 4, 6, 6); g.FillEllipse(brush, 13, 14, 6, 6);
                }
                break;
            case "update":
                g.DrawArc(p, 4, 4, 16, 16, -155, 289);
                g.DrawLines(p, [new PointF(19, 4), new PointF(19, 9), new PointF(14, 9)]);
                break;
            case "mark":
                g.DrawLines(p, [new PointF(5, 6), new PointF(12, 18), new PointF(19, 6)]);
                g.DrawLine(p, 8, 6, 16, 6);
                break;
        }
        g.Restore(s);
    }
}

sealed class Surface : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);

    public Surface()
    {
        DoubleBuffered = true;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = new Padding(16);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Width < 3 || Height < 3) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Ui.Rounded(new RectangleF(.5f, .5f, Width - 1f, Height - 1f), 13);
        using var fill = new SolidBrush(Palette.Surface);
        using var outline = new Pen(Palette.Line);
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(outline, shape);
    }
}

sealed class StudioButton : Button
{
    bool hover;
    bool pressed;
    bool selected;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal ButtonKind Kind { get; init; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string Glyph { get; init; } = "";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Selected { get => selected; set { if (selected == value) return; selected = value; Invalidate(); } }

    public StudioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        Margin = new Padding(0, 2, 0, 2);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (Width < 3 || Height < 3) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var field = Palette.Field;
        var foreground = Enabled ? Palette.Text : Palette.Muted;
        var line = Palette.Line;
        if (Kind is ButtonKind.Navigation or ButtonKind.Quiet) { field = BackColor; line = BackColor; }
        if (Kind == ButtonKind.Navigation && !Selected) foreground = Palette.Muted;
        if (Selected && Kind is ButtonKind.Navigation or ButtonKind.Toggle)
        {
            field = Palette.Mix(BackColor, Palette.Highlight, .15);
            line = Palette.Mix(BackColor, Palette.Highlight, .32);
            foreground = Palette.Highlight;
        }
        if (Kind == ButtonKind.Primary && Enabled)
        {
            field = line = Palette.Highlight;
            foreground = Palette.HighlightText;
        }
        if (Enabled && hover) field = Palette.Mix(field, Palette.Text, pressed ? .21 : .09);
        if (Focused && ShowFocusCues) line = Palette.Highlight;
        using var shape = Ui.Rounded(new RectangleF(.5f, .5f, Width - 1f, Height - 1f), 8);
        using var fill = new SolidBrush(field);
        using var border = new Pen(line);
        g.FillPath(fill, shape);
        g.DrawPath(border, shape);
        var area = new Rectangle(8, 0, Width - 16, Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        if (Glyph.Length > 0)
        {
            Ui.Glyph(g, Glyph, new RectangleF(13, (Height - 20) / 2f, 20, 20), foreground);
            area = new Rectangle(43, 0, Width - 52, Height);
        }
        else flags |= TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(g, Text, Font, area, foreground, flags);
    }
}

sealed class StudioCombo : ComboBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);

    public StudioCombo()
    {
        Dock = DockStyle.Fill;
        FlatStyle = FlatStyle.Flat;
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        IntegralHeight = false;
        DropDownHeight = 232;
        ItemHeight = 25;
        Margin = new Padding(4, 5, 4, 0);
        Tag = Tone.Field;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        var bg = selected ? Palette.Highlight : Palette.Field;
        using var brush = new SolidBrush(bg);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var bounds = Rectangle.Inflate(e.Bounds, -7, 0);
        TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString() ?? "", e.Font, bounds,
            selected ? Palette.HighlightText : Palette.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (Width < 4 || Height < 4 || !IsHandleCreated) return;
        if (message.Msg == 0x000f)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            DrawFace(graphics);
        }
        else if (message.Msg is 0x0317 or 0x0318 && message.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(message.WParam);
            DrawFace(graphics);
        }
    }

    void DrawFace(Graphics graphics)
    {
        using var background = new SolidBrush(Parent?.BackColor ?? BackColor);
        graphics.FillRectangle(background, 0, 0, Width, Height);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Ui.Rounded(new RectangleF(.5f, .5f, Width - 1, Height - 1), 7);
        using var fill = new SolidBrush(Palette.Field);
        using var border = new Pen(Focused && ShowFocusCues ? Palette.Highlight : Palette.Line);
        using var arrow = new Pen(Palette.Muted, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        graphics.FillPath(fill, shape);
        graphics.DrawPath(border, shape);
        float middle = Height / 2f;
        graphics.DrawLines(arrow, [new PointF(Width - 22, middle - 2), new PointF(Width - 17, middle + 3), new PointF(Width - 12, middle - 2)]);
        var area = new Rectangle(9, 0, Math.Max(1, Width - 36), Height);
        TextRenderer.DrawText(graphics, Text, Font, area, Enabled ? Palette.Text : Palette.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

sealed class StudioCheckBox : CheckBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(global::FlydigiKeyMapper.Appearance.Midnight, Accent.Mint);

    public StudioCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float top = (Height - 16) / 2f;
        using var track = Ui.Rounded(new RectangleF(1, top, 30, 16), 8);
        var fillColor = Checked && Enabled ? Palette.Highlight : Palette.Field;
        using var fill = new SolidBrush(fillColor);
        using var border = new Pen(Focused && ShowFocusCues ? Palette.Highlight : Palette.Line);
        using var thumb = new SolidBrush(Checked && Enabled ? Palette.HighlightText : Palette.Muted);
        g.FillPath(fill, track);
        g.DrawPath(border, track);
        g.FillEllipse(thumb, Checked ? 18 : 4, top + 3, 10, 10);
        var area = new Rectangle(40, 0, Math.Max(1, Width - 41), Height);
        TextRenderer.DrawText(g, Text, Font, area, Enabled ? Palette.Text : Palette.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

[Flags]
enum PadButton : ushort
{
    Up = 1, Down = 2, Left = 4, Right = 8,
    Menu = 0x10, View = 0x20, LeftStick = 0x40, RightStick = 0x80,
    LeftBumper = 0x100, RightBumper = 0x200,
    A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000
}

readonly record struct ControllerActivity(PadButton Buttons, Point LeftStick, Point RightStick, int LeftTrigger, int RightTrigger)
{
    public bool IsDown(PadButton button) => (Buttons & button) != 0;

    public static ControllerActivity From(Native.Pad pad) => new((PadButton)pad.buttons,
        StickOffset(pad.lx, pad.ly), StickOffset(pad.rx, pad.ry), Trigger(pad.lt), Trigger(pad.rt));

    static Point StickOffset(short rawX, short rawY)
    {
        const double idle = 1800;
        double length = Math.Sqrt((double)rawX * rawX + (double)rawY * rawY);
        if (length <= idle) return Point.Empty;
        double scale = 9 * Math.Min(1, (length - idle) / (32767 - idle)) / length;
        return new((int)Math.Round(rawX * scale), (int)Math.Round(-rawY * scale));
    }

    static int Trigger(byte value) => value <= 8 ? 0 : (value - 9) / 20 + 1;
}

sealed class ControllerDiagram : Control
{
    readonly bool[] down = new bool[6];
    ControllerActivity standard;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);

    public ControllerDiagram()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        AccessibleName = "Vader 5S front and rear views with live button, trigger, and stick activity";
    }

    public void SetActive(int index, bool value)
    {
        if (down[index] == value) return;
        down[index] = value;
        Invalidate();
    }

    public void UpdateStandardControls(Native.Pad pad)
    {
        var activity = ControllerActivity.From(pad);
        if (activity == standard) return;
        standard = activity;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        const int width = 435;
        const int height = 175;
        float scale = Math.Min((Width - 8) / (float)width, (Height - 4) / (float)height);
        if (scale <= 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.TranslateTransform((Width - width * scale) / 2, (Height - height * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(0, 24);
        DrawView(g, rear: false);
        g.TranslateTransform(225, 0);
        DrawView(g, rear: true);
    }

    void DrawView(Graphics g, bool rear)
    {
        using var body = new GraphicsPath();
        body.StartFigure();
        body.AddBezier(34, 25, 58, 10, 75, 16, 105, 16);
        body.AddBezier(105, 16, 135, 16, 152, 10, 176, 25);
        body.AddBezier(176, 25, 186, 47, 203, 92, 203, 111);
        body.AddBezier(203, 111, 204, 126, 198, 135, 183, 137);
        body.AddBezier(183, 137, 167, 123, 155, 109, 145, 108);
        body.AddBezier(145, 108, 135, 106, 75, 106, 65, 108);
        body.AddBezier(65, 108, 55, 109, 43, 123, 27, 137);
        body.AddBezier(27, 137, 12, 135, 6, 126, 7, 111);
        body.AddBezier(7, 111, 7, 92, 24, 47, 34, 25);
        body.CloseFigure();

        using var ink = new SolidBrush(Palette.Muted);
        using var rim = new Pen(Palette.Mix(Palette.Line, Palette.Highlight, .48), 1.25f) { LineJoin = LineJoin.Round };
        using var detail = new Pen(Palette.Mix(Palette.Line, Palette.Muted, .56), .85f) { LineJoin = LineJoin.Round };
        using var fill = new LinearGradientBrush(new Point(0, 11), new Point(0, 138),
            Palette.Mix(Palette.Field, Palette.Highlight, .13), Palette.Field);
        using var inset = new SolidBrush(Palette.Surface);
        using var caption = new Font("Segoe UI", 10, FontStyle.Bold, GraphicsUnit.Pixel);
        using var small = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel);
        using var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        void Shape(GraphicsPath path, int glow = 0)
        {
            using var face = new SolidBrush(glow == 0 ? Palette.Surface
                : Palette.Mix(Palette.Surface, Palette.Highlight, .54 + Math.Clamp(glow, 1, 13) * .035));
            g.FillPath(face, path);
            g.DrawPath(glow == 0 ? detail : rim, path);
        }
        void Legend(string name, RectangleF rect, bool lit)
        {
            using var ink = new SolidBrush(lit ? Palette.HighlightText : Palette.Muted);
            g.DrawString(name, small, ink, rect, center);
        }
        void Pill(RectangleF rect, string name, bool lit)
        {
            using var shape = Ui.Rounded(rect, rect.Height / 2);
            Shape(shape, lit ? 13 : 0);
            Legend(name, rect, lit);
        }
        void Circle(float x, float y, float radius, string label = "", bool lit = false, int glow = 0)
        {
            var rect = new RectangleF(x - radius, y - radius, radius * 2, radius * 2);
            using var circle = new GraphicsPath();
            circle.AddEllipse(rect);
            Shape(circle, lit ? 13 : glow);
            if (label.Length > 0) Legend(label, rect, lit);
        }
        void Stick(int x, int y, Point offset, PadButton click)
        {
            Circle(x, y, 18);
            g.DrawEllipse(detail, x - 15, y - 15, 30, 30);
            // The cap moves inside the ring, with a distinct solid highlight for a click.
            float capX = x + offset.X * .72f, capY = y + offset.Y * .72f;
            Circle(capX, capY, 10, lit: standard.IsDown(click), glow: offset.IsEmpty ? 0 : 2);
            g.DrawEllipse(detail, capX - 7, capY - 7, 14, 14);
        }
        void Direction(float x, float y, PadButton direction)
        {
            var r = new RectangleF(x - 4, y - 4, 8, 8);
            if (!standard.IsDown(direction)) return;
            using var highlight = new SolidBrush(Palette.Highlight);
            using var marker = Ui.Rounded(r, 2);
            g.FillPath(highlight, marker);
        }
        void CenterButton(float x, float y, float angle, PadButton? button, Action<Pen> icon)
        {
            var state = g.Save();
            g.TranslateTransform(x, y);
            g.RotateTransform(angle);
            bool lit = button is PadButton control && standard.IsDown(control);
            using var plate = Ui.Rounded(new RectangleF(-7, -3.5f, 14, 7), 3.5f);
            using var stroke = new Pen(lit ? Palette.HighlightText : Palette.Muted, .75f);
            Shape(plate, lit ? 13 : 0);
            icon(stroke);
            g.Restore(state);
        }
        void FrontShoulder(bool right, int pressure, PadButton bumper)
        {
            using var trigger = new GraphicsPath();
            trigger.StartFigure();
            trigger.AddBezier(34, 10, 35, -1, 37, -12, 42, -19);
            trigger.AddLine(42, -19, 58, -19);
            trigger.AddLine(58, -19, 58, 7);
            trigger.CloseFigure();
            using var shoulder = new GraphicsPath();
            shoulder.StartFigure();
            shoulder.AddBezier(23, 27, 31, 17, 51, 12, 65, 12);
            shoulder.AddBezier(65, 12, 70, 12, 74, 14, 78, 18);
            shoulder.AddLine(78, 18, 73, 22);
            shoulder.AddBezier(73, 22, 65, 16, 37, 24, 23, 32);
            shoulder.CloseFigure();
            if (right)
            {
                using var mirror = new Matrix(-1, 0, 0, 1, 210, 0);
                trigger.Transform(mirror);
                shoulder.Transform(mirror);
            }
            bool bumperPressed = standard.IsDown(bumper);
            Shape(trigger, pressure);
            Shape(shoulder, bumperPressed ? 13 : 0);
            Legend(right ? "RT" : "LT", new RectangleF(right ? 152 : 34, -11, 24, 16), pressure >= 6);
            Legend(right ? "RB" : "LB", new RectangleF(right ? 146 : 44, 13, 20, 10), bumperPressed);
        }

        g.FillPath(fill, body);
        g.DrawPath(rim, body);
        if (rear)
        {
            Pill(new RectangleF(62, 13, 20, 10), "RM", down[3]);
            Pill(new RectangleF(128, 13, 20, 10), "LM", down[2]);
            g.DrawLines(detail, [new(54, 23), new(45, 84), new(64, 107)]);
            g.DrawLines(detail, [new(156, 23), new(165, 84), new(146, 107)]);
            using var port = Ui.Rounded(new RectangleF(95, 14, 20, 5), 2);
            Shape(port);
            using var toggleLeft = Ui.Rounded(new RectangleF(65, 49, 6, 13), 3);
            using var toggleRight = Ui.Rounded(new RectangleF(139, 49, 6, 13), 3);
            g.DrawPath(detail, toggleLeft);
            g.DrawPath(detail, toggleRight);
            using var m1 = new GraphicsPath();
            m1.AddPolygon([new(50, 79), new(56, 70), new(70, 77), new(70, 86), new(54, 87)]);
            using var m2 = new GraphicsPath();
            m2.AddPolygon([new(160, 79), new(154, 70), new(140, 77), new(140, 86), new(156, 87)]);
            Shape(m1, down[4] ? 13 : 0);
            Shape(m2, down[5] ? 13 : 0);
            Legend("M1", new RectangleF(51, 73, 20, 14), down[4]);
            Legend("M2", new RectangleF(139, 73, 20, 14), down[5]);
        }
        else
        {
            FrontShoulder(false, standard.LeftTrigger, PadButton.LeftBumper);
            FrontShoulder(true, standard.RightTrigger, PadButton.RightBumper);
            g.DrawLines(detail, [new(72, 16), new(100, 47), new(105, 50), new(110, 47), new(138, 16)]);
            Circle(105, 30, 5.5f);
            g.DrawBezier(detail, 102, 27, 105, 27, 107, 31, 108, 33);
            g.DrawBezier(detail, 108, 27, 105, 27, 103, 31, 102, 33);
            Stick(55, 48, standard.LeftStick, PadButton.LeftStick);
            Stick(129, 79, standard.RightStick, PadButton.RightStick);
            Circle(80, 79, 17);
            using var cross = new GraphicsPath();
            cross.AddPolygon([new(76, 65), new(84, 65), new(84, 75), new(94, 75),
                new(94, 83), new(84, 83), new(84, 93), new(76, 93), new(76, 83),
                new(66, 83), new(66, 75), new(76, 75)]);
            Shape(cross);
            Direction(80, 70, PadButton.Up);
            Direction(80, 88, PadButton.Down);
            Direction(71, 79, PadButton.Left);
            Direction(89, 79, PadButton.Right);
            Circle(154, 36, 6.7f, "Y", standard.IsDown(PadButton.Y));
            Circle(141, 49, 6.7f, "X", standard.IsDown(PadButton.X));
            Circle(167, 49, 6.7f, "B", standard.IsDown(PadButton.B));
            Circle(154, 62, 6.7f, "A", standard.IsDown(PadButton.A));
            Circle(162, 83, 6.7f, "C", down[0]);
            Circle(175, 73, 6.7f, "Z", down[1]);
            CenterButton(89, 45, 45, PadButton.View, pen =>
            {
                g.DrawRectangle(pen, -2.7f, -.4f, 3.1f, 2);
                g.DrawLines(pen, (PointF[])[new(-.8f, -.8f), new(-.8f, -1.7f), new(2.5f, -1.7f), new(2.5f, .5f), new(1, .5f)]);
            });
            CenterButton(121, 45, -45, PadButton.Menu, pen =>
            {
                for (int y = -2; y <= 2; y += 2) g.DrawLine(pen, -2.5f, y, 2.5f, y);
            });
            CenterButton(105, 56, 0, null, pen =>
            {
                g.DrawLines(pen, (PointF[])[new(-2.7f, .2f), new(-2.7f, 1.8f), new(2.7f, 1.8f), new(2.7f, .2f)]);
                g.DrawLine(pen, 0, .8f, 0, -2.2f);
                g.DrawLines(pen, (PointF[])[new(-1.6f, -.5f), new(0, -2.2f), new(1.6f, -.5f)]);
            });
            g.DrawArc(detail, 91, 103, 6, 5, 12, 148);
            g.DrawArc(detail, 91, 103, 6, 5, 192, 148);
            g.DrawEllipse(detail, 102, 105, 6, 3);
            g.DrawArc(detail, 113, 103, 5, 5, 0, 180);
            g.DrawLine(detail, 115.5f, 108, 115.5f, 109);
            g.DrawLine(detail, 113, 108, 118, 103);
        }
        g.DrawString(rear ? "REAR" : "FRONT", caption, ink, new RectangleF(0, 136, 210, 15), center);
    }
}
