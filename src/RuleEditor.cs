using System.ComponentModel;
namespace FlydigiKeyMapper;

sealed class RuleList : ListBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Palette Palette { get; set; } = Palette.Create(Appearance.Midnight, Accent.Mint);

    public RuleList()
    {
        Dock = DockStyle.Fill;
        DrawMode = DrawMode.OwnerDrawFixed;
        IntegralHeight = false;
        BorderStyle = BorderStyle.None;
        ItemHeight = 40;
        DisplayMember = nameof(AppRule.Target);
        Margin = Padding.Empty;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        bool isRule = Items[e.Index] is AppRule;
        var background = isRule && selected ? Palette.Mix(Palette.Field, Palette.Highlight, .16) : Palette.Field;
        using var fill = new SolidBrush(background);
        e.Graphics.FillRectangle(fill, e.Bounds);
        int split = Math.Max(0, e.Bounds.Width - 115);
        var target = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, split - 14, e.Bounds.Height);
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        if (Items[e.Index] is AppRule rule)
        {
            TextRenderer.DrawText(e.Graphics, rule.Target, Font, target, Palette.Text, flags | TextFormatFlags.PathEllipsis);
            var action = new Rectangle(e.Bounds.X + split, e.Bounds.Y, 110, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, rule.Action == AppAction.Enable ? "●  Enable" : "○  Pause",
                Font, action, rule.Action == AppAction.Enable ? Palette.Highlight : Palette.Muted, flags);
            if (selected && Focused) e.DrawFocusRectangle();
        }
        else TextRenderer.DrawText(e.Graphics, "No application rules yet", Font, target, Palette.Muted, flags);
    }
}

sealed class RuleEditor : Form
{
    sealed record OpenAppChoice(string Title, ForegroundApp App)
    {
        public override string ToString() => $"{App.Executable}  ·  {Title}";
    }

    readonly TextBox target = new() { Dock = DockStyle.Fill, AccessibleName = "Executable name or path" };
    readonly StudioCombo action;
    readonly Label feedback = Ui.Text("", 9, tone: Tone.Accent);
    readonly AppRule? original;
    readonly IReadOnlyList<AppRule> existing;
    public AppRule? Rule { get; private set; }

    public RuleEditor(AppRule? original, IReadOnlyList<AppRule> existing, Palette palette)
    {
        this.original = original;
        this.existing = existing;
        Text = original is null ? "Add an application rule" : "Edit application rule";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = false;
        MinimizeBox = MaximizeBox = false;
        ClientSize = new Size(610, 336);

        var content = Ui.Rows(42, 29, 34, 29, 40, 25, 35, 28, 0);
        content.Padding = new Padding(21, 12, 21, 12);
        content.Controls.Add(Ui.Text(Text, 17, true), 0, 0);
        content.Controls.Add(Ui.Text("Pick an open app, browse to an EXE, or enter its name.", 9.5f, tone: Tone.Muted), 0, 1);

        var openApps = new StudioCombo { AccessibleName = "Open apps", Margin = new Padding(0, 1, 0, 0) };
        openApps.Items.Add("Choose an open app…");
        foreach (var app in ForegroundTracker.OpenApps())
            openApps.Items.Add(new OpenAppChoice(app.Title, app.App));
        openApps.SelectedIndex = 0;
        openApps.SelectedIndexChanged += (_, _) =>
        {
            if (openApps.SelectedItem is OpenAppChoice choice)
                target.Text = choice.App.Path.Length > 0 ? choice.App.Path : choice.App.Executable;
        };
        content.Controls.Add(openApps, 0, 2);

        content.Controls.Add(Ui.Text("Executable name or full path", 9.5f, true), 0, 3);
        var input = Ui.Columns(-100, 108);
        target.Text = original?.Target ?? "";
        target.Margin = new Padding(0, 6, 9, 0);
        target.PlaceholderText = "Example  notepad.exe";
        input.Controls.Add(target, 0, 0);
        var browse = Ui.Button("Browse…", (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", CheckFileExists = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.FileName;
        });
        browse.Margin = new Padding(0, 2, 0, 5);
        input.Controls.Add(browse, 1, 0);
        content.Controls.Add(input, 0, 4);

        content.Controls.Add(Ui.Text("When this app is focused", 9.5f, true), 0, 5);
        action = Mapper.ChoiceBox(["Enable mappings", "Pause mappings"], (int)(original?.Action ?? AppAction.Enable), _ => { });
        action.AccessibleName = "Rule action";
        action.Margin = new Padding(0, 2, 0, 0);
        content.Controls.Add(action, 0, 6);
        content.Controls.Add(feedback, 0, 7);

        var buttons = Ui.Columns(-100, 98, 112);
        var cancel = Ui.Button("Cancel", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        cancel.Margin = new Padding(0, 1, 8, 1);
        buttons.Controls.Add(cancel, 1, 0);
        var save = Ui.Button("Save rule", (_, _) => SaveRule(), ButtonKind.Primary);
        save.Margin = new Padding(0, 1, 0, 1);
        buttons.Controls.Add(save, 2, 0);
        content.Controls.Add(buttons, 0, 8);
        Controls.Add(content);
        AcceptButton = save;
        CancelButton = cancel;
        ActiveControl = target;
        Ui.Theme(this, palette, palette.Background);
    }

    void SaveRule()
    {
        string value = AppRouting.NormalizeTarget(target.Text);
        if (value.Length == 0)
        {
            feedback.Text = "Enter an executable name or an absolute path to an EXE.";
            target.Focus();
            return;
        }
        if (existing.Any(rule => rule != original && rule.Target.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            feedback.Text = "This executable already has a rule.";
            target.Focus();
            return;
        }
        Rule = new AppRule { Target = value, Action = (AppAction)action.SelectedIndex };
        DialogResult = DialogResult.OK;
        Close();
    }
}
