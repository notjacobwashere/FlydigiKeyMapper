using System.Drawing.Imaging;

namespace FlydigiKeyMapper;

sealed partial class Mapper
{
    static readonly string[] PageNames = ["Mappings", "App rules", "Settings", "Updates"];
    static readonly string[] PageGlyphs = ["mapping", "apps", "settings", "update"];
    static readonly string[] PageDescriptions =
    [
        "Assign a keyboard key to each of your Vader 5S extra buttons.",
        "Choose where mappings work based on the active window.",
        "Personalize the app and control how it runs in Windows.",
        "Check for a new version and manage the required components."
    ];
    readonly StudioButton[] navButtons = new StudioButton[4];
    readonly Control[] pages = new Control[4];
    readonly Label pageTitle = Ui.Text("", 24, true);
    readonly Label pageDescription = Ui.Text("", 10.5f, tone: Tone.Muted);
    readonly Label feedback = Ui.Text("", 9.5f, tone: Tone.Muted);
    readonly Label controllerState = Ui.Text("Waiting for USB input…", 9.5f, tone: Tone.Muted);
    readonly Label routeState = Ui.Text("", 9.5f, tone: Tone.Muted);
    readonly Label focusedAppLabel = Ui.Text("", 10.5f, true);
    readonly Label appDecisionLabel = Ui.Text("", 9.5f, tone: Tone.Muted);
    readonly StudioCheckBox startupSwitch = new();
    readonly Panel pageHost = new() { Dock = DockStyle.Fill };
    StudioButton master = null!;
    ToolStripMenuItem? trayToggle;
    StudioButton editRule = null!;
    StudioButton removeRule = null!;
    RuleList rules = null!;
    Palette palette = Palette.Create(Appearance.Midnight, Accent.Mint);
    int pageIndex;

    void BuildStudio()
    {
        Text = "Flydigi Key Mapper";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1110, 743);
        MinimumSize = new Size(900, 620);
        DoubleBuffered = true;
        SuspendLayout();

        var shell = Ui.Columns(223, -100);
        shell.Controls.Add(BuildSidebar(), 0, 0);

        var content = Ui.Rows(43, 29, 15, 0, 32);
        content.Padding = new Padding(28, 21, 28, 11);
        var heading = Ui.Columns(-100, 153);
        heading.Controls.Add(pageTitle, 0, 0);
        master = Ui.Button("", (_, _) => SetMappingEnabled(!settings.MappingEnabled), ButtonKind.Toggle);
        master.Margin = new Padding(0, 5, 0, 3);
        heading.Controls.Add(master, 1, 0);
        content.Controls.Add(heading, 0, 0);
        content.Controls.Add(pageDescription, 0, 1);
        content.Controls.Add(pageHost, 0, 3);
        content.Controls.Add(feedback, 0, 4);
        shell.Controls.Add(content, 1, 0);
        Controls.Add(shell);

        pages[0] = BuildMappings();
        pages[1] = BuildRules();
        pages[2] = BuildPreferences();
        pages[3] = new UpdatesPage();
        foreach (Control page in pages)
        {
            page.Visible = false;
            pageHost.Controls.Add(page);
        }
        ApplyTheme();
        ShowPage(0);
        ResumeLayout(true);
    }

    Control BuildSidebar()
    {
        var sidebar = Ui.Rows(64, 32, 43, 43, 43, 43, 0, 94, 25);
        sidebar.Tag = Tone.Sidebar;
        sidebar.Padding = new Padding(17, 22, 17, 16);

        var identity = Ui.Columns(41, -100);
        var mark = new BrandMark();
        mark.Margin = new Padding(0, 9, 4, 9);
        identity.Controls.Add(mark, 0, 0);
        var name = Ui.Rows(7, 27, 22, 0);
        name.Padding = new Padding(6, 0, 0, 0);
        name.Controls.Add(Ui.Text("FLYDIGI", 14, true), 0, 1);
        name.Controls.Add(Ui.Text("KEY MAPPER", 9, true, Tone.Muted), 0, 2);
        identity.Controls.Add(name, 1, 0);
        sidebar.Controls.Add(identity, 0, 0);

        for (int i = 0; i < navButtons.Length; i++)
        {
            int selected = i;
            var button = new StudioButton
            {
                Text = PageNames[i], Glyph = PageGlyphs[i], Dock = DockStyle.Fill,
                Kind = ButtonKind.Navigation
            };
            button.Click += (_, _) => ShowPage(selected);
            navButtons[i] = button;
            sidebar.Controls.Add(button, 0, i + 2);
        }

        var statusCard = new Surface { Padding = new Padding(10), Margin = new Padding(0, 0, 0, 8) };
        var status = Ui.Rows(20, 24, 0);
        status.Controls.Add(Ui.Text("FOREGROUND", 8, true, Tone.Muted), 0, 0);
        status.Controls.Add(focusedAppLabel, 0, 1);
        status.Controls.Add(routeState, 0, 2);
        statusCard.Controls.Add(status);
        sidebar.Controls.Add(statusCard, 0, 7);
        sidebar.Controls.Add(Ui.Text($"Version {UpdateService.Current.ToString(3)}", 8.5f, tone: Tone.Muted), 0, 8);
        return sidebar;
    }

    Control BuildMappings()
    {
        var hero = new Surface { Padding = new Padding(20, 11, 14, 10) };
        var heroColumns = Ui.Columns(-54, -46);
        var details = Ui.Rows(25, 23, 47, 23, 0);
        details.Controls.Add(Ui.Text("CONTROLLER  /  USB", 9, true, Tone.Accent), 0, 1);
        details.Controls.Add(Ui.Text("Vader 5S", 27, true), 0, 2);
        details.Controls.Add(controllerState, 0, 3);
        details.Controls.Add(Ui.Text("Press an extra button to see it light up.", 9, tone: Tone.Muted), 0, 4);
        heroColumns.Controls.Add(details, 0, 0);
        heroColumns.Controls.Add(diagram, 1, 0);
        hero.Controls.Add(heroColumns);

        var bindings = new Surface { Padding = new Padding(17, 12, 17, 11) };
        var rows = Ui.Rows(35, 35, 43, 43, 43, 43, 43, 43);
        rows.Controls.Add(Ui.Text("Button assignments", 14, true), 0, 0);
        var headings = BindingColumns();
        headings.Controls.Add(Ui.Text("BUTTON", 8, true, Tone.Muted), 0, 0);
        headings.Controls.Add(Ui.Text("INPUT STATUS", 8, true, Tone.Muted), 1, 0);
        headings.Controls.Add(Ui.Text("KEYBOARD OUTPUT", 8, true, Tone.Muted), 2, 0);
        rows.Controls.Add(headings, 0, 1);

        for (int i = 0; i < settings.Maps.Count; i++)
        {
            int row = i;
            var map = settings.Maps[row];
            var strip = BindingColumns();
            var buttonName = Ui.Text(map.Name, 12, true, Tone.Accent);
            buttonName.Margin = new Padding(2, 0, 0, 0);
            strip.Controls.Add(buttonName, 0, 0);
            var activity = Ui.Text("○  Ready", 9.5f, tone: Tone.Muted);
            indicators[row] = activity;
            strip.Controls.Add(activity, 1, 0);

            var box = new StudioCombo
            {
                Margin = new Padding(0, 6, 10, 0), AccessibleName = $"{map.Name} keyboard output"
            };
            keyBoxes[row] = box;
            var choices = new[] { new KeyChoice(0), new KeyChoice(0x1000) }
                .Concat(commonKeys.Select(key => new KeyChoice((int)key))).ToList();
            if (!choices.Any(choice => choice.Code == map.Key)) choices.Add(new KeyChoice(map.Key));
            box.Items.AddRange(choices.Cast<object>().ToArray());
            box.SelectedItem = choices.First(item => item.Code == map.Key);
            box.SelectedIndexChanged += (_, _) =>
            {
                if (box.SelectedItem is not KeyChoice chosen) return;
                InterruptOutput();
                map.Key = chosen.Code;
                Save();
            };
            strip.Controls.Add(box, 2, 0);
            var listen = Ui.Button("Listen", (_, _) =>
            {
                if (listening == row) CancelListening();
                else StartListening(row);
            });
            listen.Margin = new Padding(0, 4, 7, 5);
            listen.AccessibleName = $"Listen for {map.Name} keyboard output";
            listenButtons[row] = listen;
            strip.Controls.Add(listen, 3, 0);
            var clear = Ui.Button("Clear", (_, _) => { CancelListening(); box.SelectedIndex = 0; }, ButtonKind.Quiet);
            clear.Margin = new Padding(0, 4, 0, 5);
            clear.AccessibleName = $"Clear {map.Name} mapping";
            strip.Controls.Add(clear, 4, 0);
            rows.Controls.Add(strip, 0, i + 2);
        }

        bindings.Controls.Add(rows);
        var page = Ui.Rows(169, 13, 356, 0);
        page.Controls.Add(hero, 0, 0);
        page.Controls.Add(bindings, 0, 2);
        return ScrollPage(page, 538);
    }

    static TableLayoutPanel BindingColumns() => Ui.Columns(82, -40, -60, 82, 65);

    Control BuildRules()
    {
        var routing = new Surface();
        var options = Ui.Rows(27, 42, 24, 31);
        options.Controls.Add(Ui.Text("Default behavior", 14, true), 0, 0);
        var picker = Ui.Columns(261, -100);
        var scope = ChoiceBox(
            ["Enable in all apps", "Pause unless a rule enables", "Enable in GTA / FiveM"],
            (int)settings.EffectiveScope, index =>
            {
                InterruptOutput();
                settings.Scope = (AppScope)index;
                Save();
                RefreshRouting();
            });
        scope.Margin = new Padding(0, 5, 12, 0);
        scope.AccessibleName = "Default app routing behavior";
        picker.Controls.Add(scope, 0, 0);
        picker.Controls.Add(appDecisionLabel, 1, 0);
        options.Controls.Add(picker, 0, 1);
        options.Controls.Add(Ui.Text("Rules apply when their app is focused. A full path overrides an executable-name match.", 9, tone: Tone.Muted), 0, 2);
        options.Controls.Add(Ui.Text("Use “Pause unless a rule enables” to build an allow list.", 9, tone: Tone.Muted), 0, 3);
        routing.Controls.Add(options);

        var ruleCard = new Surface();
        var listContent = Ui.Rows(43, 27, 0, 39);
        var listHeader = Ui.Columns(-100, 122);
        listHeader.Controls.Add(Ui.Text("Application rules", 14, true), 0, 0);
        var add = Ui.Button("+ Add rule", (_, _) => EditAppRule(null), ButtonKind.Primary);
        add.Margin = new Padding(0, 2, 0, 5);
        listHeader.Controls.Add(add, 1, 0);
        listContent.Controls.Add(listHeader, 0, 0);

        var columnLabels = Ui.Columns(-100, 115);
        var exeLabel = Ui.Text("APPLICATION / EXECUTABLE", 8, true, Tone.Muted);
        exeLabel.Margin = new Padding(9, 0, 0, 0);
        columnLabels.Controls.Add(exeLabel, 0, 0);
        columnLabels.Controls.Add(Ui.Text("MAPPING", 8, true, Tone.Muted), 1, 0);
        listContent.Controls.Add(columnLabels, 0, 1);

        rules = new RuleList { AccessibleName = "Application rules" };
        rules.DoubleClick += (_, _) => { if (rules.SelectedItem is AppRule rule) EditAppRule(rule); };
        rules.SelectedIndexChanged += (_, _) => UpdateRuleActions();
        listContent.Controls.Add(rules, 0, 2);

        var actions = Ui.Columns(-100, 97, 103);
        editRule = Ui.Button("Edit", (_, _) => { if (rules.SelectedItem is AppRule rule) EditAppRule(rule); });
        editRule.Margin = new Padding(0, 6, 8, 0);
        actions.Controls.Add(editRule, 1, 0);
        removeRule = Ui.Button("Remove", (_, _) =>
        {
            if (rules.SelectedItem is not AppRule rule) return;
            InterruptOutput();
            settings.AppRules.Remove(rule);
            Save();
            PopulateRules();
            RefreshRouting();
        });
        removeRule.Margin = new Padding(0, 6, 0, 0);
        actions.Controls.Add(removeRule, 2, 0);
        listContent.Controls.Add(actions, 0, 3);

        ruleCard.Controls.Add(listContent);
        PopulateRules();
        var screen = Ui.Rows(154, 13, 367, 0);
        screen.Controls.Add(routing, 0, 0);
        screen.Controls.Add(ruleCard, 0, 2);
        return ScrollPage(screen, 534);
    }

    void PopulateRules(AppRule? selected = null)
    {
        rules.BeginUpdate();
        rules.Items.Clear();
        if (settings.AppRules.Count == 0) rules.Items.Add("No application rules yet");
        else
            foreach (var rule in settings.AppRules) rules.Items.Add(rule);
        if (selected is not null) rules.SelectedItem = selected;
        rules.EndUpdate();
        UpdateRuleActions();
    }

    void UpdateRuleActions()
    {
        bool selected = rules.SelectedItem is AppRule;
        editRule.Enabled = removeRule.Enabled = selected;
    }

    void EditAppRule(AppRule? original)
    {
        using var editor = new RuleEditor(original, settings.AppRules, palette);
        if (editor.ShowDialog(this) != DialogResult.OK || editor.Rule is not AppRule updated) return;
        InterruptOutput();
        if (original is null) settings.AppRules.Add(updated);
        else settings.AppRules[settings.AppRules.IndexOf(original)] = updated;
        Save();
        PopulateRules(updated);
        RefreshRouting();
    }

    Control BuildPreferences()
    {
        var appearance = new Surface();
        var visual = Ui.Rows(32, 33, 39);
        visual.Controls.Add(Ui.Text("Appearance", 14, true), 0, 0);
        var labels = Ui.Columns(-50, -50);
        labels.Controls.Add(Ui.Text("Theme", 10, true), 0, 0);
        labels.Controls.Add(Ui.Text("Accent color", 10, true), 1, 0);
        visual.Controls.Add(labels, 0, 1);
        var pickers = Ui.Columns(-50, -50);
        var theme = ChoiceBox(["Midnight", "Slate", "Light"], (int)settings.Appearance, index =>
        {
            settings.Appearance = (Appearance)index;
            Save();
            ApplyTheme();
        });
        theme.AccessibleName = "Theme";
        pickers.Controls.Add(theme, 0, 0);
        var accent = ChoiceBox(["Mint", "Blue", "Violet", "Amber"], (int)settings.Accent, index =>
        {
            settings.Accent = (Accent)index;
            Save();
            ApplyTheme();
        });
        accent.AccessibleName = "Accent color";
        pickers.Controls.Add(accent, 1, 0);
        visual.Controls.Add(pickers, 0, 2);
        appearance.Controls.Add(visual);

        var behaviorCard = new Surface();
        var behavior = Ui.Rows(33, 35, 35, 35, 35);
        behavior.Controls.Add(Ui.Text("Windows behavior", 14, true), 0, 0);
        SetSwitch(startupSwitch, "Start with Windows", false, ChangeStartup);
        startupSwitch.Enabled = hardware;
        behavior.Controls.Add(PreferenceRow(startupSwitch, "Starts in the tray when close-to-tray is enabled."), 0, 1);
        var close = new StudioCheckBox();
        var notice = new StudioCheckBox();
        SetSwitch(close, "Keep running when closed", settings.CloseToTray, () =>
        {
            settings.CloseToTray = close.Checked;
            notice.Enabled = close.Checked;
            Save();
        });
        behavior.Controls.Add(PreferenceRow(close, "Use the tray icon to open, pause, or quit."), 0, 2);
        SetSwitch(notice, "Show tray notifications", settings.TrayNotice, () => { settings.TrayNotice = notice.Checked; Save(); });
        notice.Enabled = close.Checked;
        behavior.Controls.Add(PreferenceRow(notice, "Show a notice when the window moves to the tray."), 0, 3);
        var log = new StudioCheckBox();
        SetSwitch(log, "Log controller input", settings.LogButtons, () => { settings.LogButtons = log.Checked; Save(); });
        behavior.Controls.Add(PreferenceRow(log, "Save detected controller presses for diagnostics."), 0, 4);
        behaviorCard.Controls.Add(behavior);

        var diagnostics = new Surface();
        var tools = Ui.Rows(31, 28, 43);
        tools.Controls.Add(Ui.Text("Diagnostics", 14, true), 0, 0);
        tools.Controls.Add(Ui.Text("View the device report or open the folder that stores your settings and logs.", 9, tone: Tone.Muted), 0, 1);
        var actions = Ui.Columns(152, 168, -100);
        var report = Ui.Button("Open device report", (_, _) => WriteDeviceReport());
        report.Margin = new Padding(0, 4, 8, 2);
        actions.Controls.Add(report, 0, 0);
        var open = Ui.Button("Open settings folder", (_, _) => OpenTarget(AppPaths.DataDirectory));
        open.Margin = new Padding(0, 4, 0, 2);
        actions.Controls.Add(open, 1, 0);
        tools.Controls.Add(actions, 0, 2);
        diagnostics.Controls.Add(tools);

        var page = Ui.Rows(137, 13, 206, 13, 135, 0);
        page.Controls.Add(appearance, 0, 0);
        page.Controls.Add(behaviorCard, 0, 2);
        page.Controls.Add(diagnostics, 0, 4);
        return ScrollPage(page, 504);
    }

    static Control PreferenceRow(CheckBox checkbox, string detail)
    {
        var row = Ui.Columns(234, -100);
        row.Controls.Add(checkbox, 0, 0);
        row.Controls.Add(Ui.Text(detail, 9, tone: Tone.Muted), 1, 0);
        return row;
    }

    static void SetSwitch(CheckBox checkbox, string text, bool value, Action changed)
    {
        checkbox.Text = text;
        checkbox.Dock = DockStyle.Fill;
        checkbox.Margin = Padding.Empty;
        checkbox.Checked = value;
        checkbox.CheckedChanged += (_, _) => changed();
    }

    internal static StudioCombo ChoiceBox(string[] choices, int selected, Action<int> change)
    {
        var combo = new StudioCombo();
        combo.Items.AddRange(choices);
        combo.SelectedIndex = selected;
        combo.SelectedIndexChanged += (_, _) => { if (combo.SelectedIndex >= 0) change(combo.SelectedIndex); };
        return combo;
    }

    internal static Panel ScrollPage(TableLayoutPanel surface, int height)
    {
        var pane = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        surface.Dock = DockStyle.Top;
        surface.Height = height;
        pane.Controls.Add(surface);
        return pane;
    }

    void ShowPage(int index)
    {
        if (pageIndex != index) CancelListening();
        pageIndex = index;
        pageHost.SuspendLayout();
        for (int i = 0; i < pages.Length; i++)
        {
            navButtons[i].Selected = i == index;
            pages[i].Visible = i == index;
        }
        pageTitle.Text = PageNames[index];
        pageDescription.Text = PageDescriptions[index];
        pageHost.ResumeLayout();
    }

    void RenderRoute()
    {
        master.Text = settings.MappingEnabled ? "●  Mapper on" : "○  Mapper off";
        master.Selected = settings.MappingEnabled;
        master.AccessibleName = settings.MappingEnabled ? "Pause the mapper" : "Enable the mapper";
        if (trayToggle is not null) trayToggle.Text = settings.MappingEnabled ? "Pause mapping" : "Enable mapping";
        focusedAppLabel.Text = currentApp.DisplayName;
        routeState.Text = settings.MappingEnabled && routeAllowed ? "●  Mapping active" : "○  Mapping paused";
        routeState.ForeColor = settings.MappingEnabled && routeAllowed ? palette.Highlight : palette.Muted;
        appDecisionLabel.Text = !settings.MappingEnabled ? "Global mapper is off"
            : $"{currentApp.DisplayName}  /  {(routeAllowed ? "Enabled" : "Paused")}";
    }

    void ApplyTheme()
    {
        palette = Palette.Create(settings.Appearance, settings.Accent);
        Ui.Theme(this, palette, palette.Background);
        rules.Palette = palette;
        tray.ContextMenuStrip?.Invalidate();
        RenderRoute();
    }

    void Report(string text)
    {
        if (!feedback.IsDisposed && feedback.Text != text) feedback.Text = text;
    }

    public void RenderPreview()
    {
        Show();
        Application.DoEvents();
        currentApp = new ForegroundApp(IntPtr.Zero, 0, "FlydigiKeyMapper.exe", "");
        controllerState.Text = "Vader 5S USB input preview";
        RenderRoute();
        SavePreviews();
        settings.Appearance = Appearance.Light;
        ApplyTheme();
        ShowPage(0);
        CapturePage("light");
    }

    void SavePreviews()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            ShowPage(i);
            CapturePage(PageNames[i].Replace(" ", "-").ToLowerInvariant());
        }
    }

    void CapturePage(string name)
    {
        Application.DoEvents();
        using var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        Controls[0].DrawToBitmap(bitmap, new Rectangle(Point.Empty, ClientSize));
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, name + "-preview.png"), ImageFormat.Png);
    }
}

sealed class BrandMark : Control
{
    public BrandMark()
    {
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var size = Math.Min(Width, Height) - 2;
        if (size <= 4) return;
        var box = new RectangleF(1, (Height - size) / 2f, size, size);
        using var shape = Ui.Rounded(box, 9);
        using var fill = new SolidBrush(Palette.Mix(BackColor, ForeColor, .15));
        g.FillPath(fill, shape);
        box.Inflate(-5, -5);
        Ui.Glyph(g, "mark", box, ForeColor);
    }
}
