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

sealed class ControllerDiagram : Control
{
    static readonly (float X, float Y)[] Positions = [(243, 79), (272, 79), (64, 31), (259, 31), (94, 104), (228, 104)];
    readonly bool[] down = new bool[6];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);

    public ControllerDiagram()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        ResizeRedraw = true;
        AccessibleName = "Vader 5S extra button activity";
    }

    public void SetActive(int index, bool value)
    {
        if (down[index] == value) return;
        down[index] = value;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float scale = Math.Min((Width - 8) / 324f, (Height - 4) / 145f);
        if (scale <= 0) return;
        g.TranslateTransform((Width - 324 * scale) / 2, (Height - 145 * scale) / 2);
        g.ScaleTransform(scale, scale);
        using var body = new GraphicsPath();
        body.StartFigure();
        body.AddBezier(22, 96, 24, 67, 35, 24, 55, 17);
        body.AddBezier(55, 17, 80, 4, 108, 20, 162, 19);
        body.AddBezier(162, 19, 216, 20, 244, 4, 269, 17);
        body.AddBezier(269, 17, 289, 24, 300, 67, 302, 96);
        body.AddBezier(302, 96, 310, 147, 277, 146, 241, 115);
        body.AddBezier(241, 115, 220, 98, 218, 95, 162, 96);
        body.AddBezier(162, 96, 106, 95, 104, 98, 83, 115);
        body.AddBezier(83, 115, 47, 146, 14, 147, 22, 96);
        body.CloseFigure();
        using var fill = new LinearGradientBrush(new Point(0, 10), new Point(0, 140),
            Palette.Mix(Palette.Surface, Palette.Highlight, .13), Palette.Field);
        using var outline = new Pen(Palette.Mix(Palette.Line, Palette.Highlight, .34), 1.7f);
        using var recessed = new SolidBrush(Palette.Surface);
        using var details = new Pen(Palette.Muted, 1.2f);
        g.FillPath(fill, body);
        g.DrawPath(outline, body);
        g.FillEllipse(recessed, 82, 44, 31, 31);
        g.DrawEllipse(outline, 82, 44, 31, 31);
        g.DrawEllipse(details, 88, 50, 19, 19);
        g.FillEllipse(recessed, 177, 54, 31, 31);
        g.DrawEllipse(outline, 177, 54, 31, 31);
        g.DrawEllipse(details, 183, 60, 19, 19);
        g.DrawLine(details, 62, 79, 62, 99);
        g.DrawLine(details, 52, 89, 72, 89);
        foreach (var point in new[] { (232, 49), (244, 37), (256, 49), (244, 61) })
            g.DrawEllipse(outline, point.Item1 - 4, point.Item2 - 4, 8, 8);
        Ui.Glyph(g, "mark", new RectangleF(151, 40, 22, 22), Palette.Highlight);
        using var font = new Font("Segoe UI", 8, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        for (int i = 0; i < Positions.Length; i++)
        {
            var (x, y) = Positions[i];
            var r = new RectangleF(x - 12, y - 8, 24, 16);
            using var chip = Ui.Rounded(r, 6);
            using var plate = new SolidBrush(down[i] ? Palette.Highlight : Palette.Surface);
            using var ink = new SolidBrush(down[i] ? Palette.HighlightText : Palette.Highlight);
            g.FillPath(plate, chip);
            g.DrawPath(outline, chip);
            g.DrawString(VendorInput.Names[i], font, ink, r, format);
        }
    }
}
