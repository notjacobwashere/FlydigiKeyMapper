using System.Diagnostics;


namespace FlydigiKeyMapper;

sealed partial class Mapper : Form, IMessageFilter
{
    readonly string config = AppPaths.Configuration;
    readonly Settings settings;
    readonly VendorInput? vendor;
    readonly ForegroundTracker foreground = new();
    readonly System.Windows.Forms.Timer pollTimer = new() { Interval = 10 };
    readonly NotifyIcon tray = new();
    readonly HashSet<string> active = new(StringComparer.Ordinal);
    readonly HashSet<string> blockedUntilReleased = new(StringComparer.Ordinal);
    readonly HashSet<string> seen = new(StringComparer.Ordinal);
    readonly HashSet<int> held = [];
    readonly StudioCombo[] keyBoxes = new StudioCombo[6];
    readonly StudioButton[] listenButtons = new StudioButton[6];
    readonly Label[] indicators = new Label[6];
    readonly ControllerDiagram diagram = new();
    readonly bool hardware;
    readonly Keys[] commonKeys =
    [
        .. Enumerable.Range(65, 26).Select(code => (Keys)code),
        .. Enumerable.Range(48, 10).Select(code => (Keys)code),
        .. Enumerable.Range(96, 10).Select(code => (Keys)code),
        Keys.Add, Keys.Subtract, Keys.Multiply, Keys.Divide, Keys.Decimal,
        .. Enumerable.Range(112, 24).Select(code => (Keys)code),
        Keys.Space, Keys.Tab, Keys.Enter, Keys.Escape, Keys.Back, Keys.Insert, Keys.Delete,
        Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown, Keys.Up, Keys.Down, Keys.Left, Keys.Right,
        Keys.LShiftKey, Keys.RShiftKey, Keys.LControlKey, Keys.RControlKey, Keys.LMenu, Keys.RMenu
    ];
    bool loading = true;
    bool exiting;
    bool routeAllowed;
    bool outputAllowed;
    bool startupRead;
    int listening = -1;
    int capturedVk = -1;
    long listenDeadline;
    long nextDeviceCheck;
    string lastVendorStatus = "";
    ForegroundApp currentApp = new(IntPtr.Zero, 0, "", "");
    AppRule? matchedRule;

    public Mapper(bool hidden, bool hardware = true)
    {
        this.hardware = hardware;
        string? loadError = null;
        try { settings = Settings.Load(config); }
        catch (Exception ex)
        {
            settings = new();
            settings.Normalize();
            loadError = "Could not read settings. " + ex.Message;
        }
        vendor = hardware ? new VendorInput() : null;
        BuildStudio();
        if (loadError is not null) Report(loadError);
        if (hardware) ConfigureTray();
        SyncRoute();
        if (hardware) RenderRoute();
        Application.AddMessageFilter(this);
        Deactivate += (_, _) => CancelListening();
        FormClosing += CloseWindow;
        Shown += (_, _) =>
        {
            if (hidden && settings.CloseToTray) Hide();
            if (hardware)
            {
                Native.Register(Handle);
                ReadStartup();
            }
        };
        if (hardware)
        {
            pollTimer.Tick += (_, _) => PollInputs();
            pollTimer.Start();
        }
        loading = false;
    }

    void ConfigureTray()
    {
        tray.Icon = SystemIcons.Application;
        tray.Text = "Flydigi Key Mapper";
        tray.ContextMenuStrip = new ContextMenuStrip();
        tray.ContextMenuStrip.Items.Add("Open Flydigi Key Mapper", null, (_, _) => OpenWindow());
        trayToggle = new ToolStripMenuItem();
        trayToggle.Click += (_, _) => SetMappingEnabled(!settings.MappingEnabled);
        tray.ContextMenuStrip.Items.Add(trayToggle);
        tray.ContextMenuStrip.Items.Add(new ToolStripSeparator());
        tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => ExitMapper());
        tray.DoubleClick += (_, _) => OpenWindow();
        tray.Visible = true;
    }

    void OpenWindow()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void ExitMapper()
    {
        exiting = true;
        Application.Exit();
    }

    void CloseWindow(object? sender, FormClosingEventArgs e)
    {
        InterruptOutput();
        if (!exiting && hardware && settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            CancelListening();
            Hide();
            if (settings.TrayNotice)
                tray.ShowBalloonTip(2000, "Flydigi Key Mapper", "Running in the tray. Use its menu to pause mapping or exit.", ToolTipIcon.Info);
        }
    }

    bool Save()
    {
        if (loading) return true;
        try { settings.Save(config); return true; }
        catch (Exception error)
        {
            Report("Could not save settings. " + error.Message);
            return false;
        }
    }

    void SetMappingEnabled(bool enabled)
    {
        if (settings.MappingEnabled == enabled) return;
        InterruptOutput();
        settings.MappingEnabled = enabled;
        Save();
        RefreshRouting();
    }

    void ReadStartup()
    {
        try
        {
            bool legacy = StartupTask.LegacyEnabled;
            bool runningAtLogin = StartupTask.Enabled || legacy;
            if (legacy) StartupTask.Set(true);
            loading = true;
            startupSwitch.Checked = runningAtLogin;
            startupRead = true;
        }
        catch (Exception ex) { Report("Could not read Windows startup. " + ex.Message); }
        finally { loading = false; }
    }

    void ChangeStartup()
    {
        if (loading || !hardware) return;
        try
        {
            StartupTask.Set(startupSwitch.Checked);
            startupRead = true;
        }
        catch (Exception ex)
        {
            loading = true;
            startupSwitch.Checked = startupRead && !startupSwitch.Checked;
            loading = false;
            Report("Could not change Windows startup. " + ex.Message);
        }
    }

    void WriteDeviceReport()
    {
        try
        {
            var path = AppPaths.FilePath("diagnostics.txt");
            File.WriteAllText(path, Native.Devices() + Environment.NewLine + vendor?.Info
                + Environment.NewLine + "Observed inputs" + Environment.NewLine + string.Join(Environment.NewLine, seen));
            OpenTarget(path);
        }
        catch (Exception ex) { Report("Could not write the device report. " + ex.Message); }
    }

    void OpenTarget(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Report("Could not open the item. " + ex.Message); }
    }

    void RefreshRouting()
    {
        SyncRoute();
        RenderRoute();
    }

    void SyncRoute()
    {
        currentApp = foreground.Read();
        matchedRule = AppRouting.Match(settings.AppRules, currentApp);
        routeAllowed = AppRouting.Allows(settings.EffectiveScope, matchedRule, currentApp.Executable);
    }

    void UpdateSource(string prefix, IEnumerable<string> inputs)
    {
        var next = inputs.ToHashSet(StringComparer.Ordinal);
        foreach (string input in next)
        {
            if (active.Contains(input)) continue;
            seen.Add(input);
            if (!settings.LogButtons) continue;
            try { File.AppendAllText(AppPaths.FilePath("button-events.txt"), $"{DateTime.Now:O} {input}{Environment.NewLine}"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        active.RemoveWhere(input => input.StartsWith(prefix, StringComparison.Ordinal));
        active.UnionWith(next);
    }

    void PollInputs()
    {
        var now = Environment.TickCount64;
        if (listening >= 0 && now > listenDeadline) CancelListening();
        UpdateSource("V:", vendor?.Poll() ?? []);
        for (uint i = 0; i < 4; i++)
        {
            var buttons = new List<string>();
            if (Native.XInputGetState(i, out var state) == 0)
            {
                for (int bit = 0; bit < 16; bit++)
                    if ((state.pad.buttons & (1 << bit)) != 0) buttons.Add($"X:{i}:{bit}");
                if (state.pad.lt > 160) buttons.Add($"X:{i}:LT");
                if (state.pad.rt > 160) buttons.Add($"X:{i}:RT");
            }
            UpdateSource($"X:{i}:", buttons);
        }
        if (now >= nextDeviceCheck)
        {
            nextDeviceCheck = now + 1000;
            active.RemoveWhere(input => input.StartsWith("H:", StringComparison.Ordinal)
                && !Native.DeviceExists(input.Split('|')[0][2..]));
        }

        // Read the foreground immediately before deciding which keys to inject.
        var previousApp = currentApp;
        var previousRule = matchedRule;
        bool previousRoute = routeAllowed;
        SyncRoute();
        if (previousApp != currentApp || previousRule != matchedRule || previousRoute != routeAllowed)
            RenderRoute();
        if (previousRule != matchedRule && previousApp.ProcessId != currentApp.ProcessId)
            InterruptOutput();

        var status = vendor?.Info ?? "Hardware disabled in preview";
        if (status != lastVendorStatus)
        {
            lastVendorStatus = status;
            controllerState.Text = status;
        }
        ApplyMappings(settings.MappingEnabled && routeAllowed && listening < 0 && capturedVk < 0);
    }

    void ApplyMappings(bool allow)
    {
        blockedUntilReleased.RemoveWhere(source => !active.Contains(source));
        if (!allow)
        {
            // Remember presses while paused so enabling or switching windows cannot send a stale key-down.
            foreach (var mapping in settings.Maps)
                if (active.Contains(mapping.Source)) blockedUntilReleased.Add(mapping.Source);
            if (outputAllowed) ReleaseAll();
        }
        outputAllowed = allow;
        var desired = new HashSet<int>();
        for (int i = 0; i < settings.Maps.Count; i++)
        {
            var mapping = settings.Maps[i];
            bool down = active.Contains(mapping.Source);
            if (diagram.Visible) diagram.SetActive(i, down);
            var indicator = indicators[i];
            string activity = down ? "●  Pressed" : "○  Ready";
            if (indicator.Text != activity)
            {
                indicator.Text = activity;
                indicator.ForeColor = down ? palette.Highlight : palette.Muted;
            }
            if (allow && down && mapping.Key != 0 && !blockedUntilReleased.Contains(mapping.Source)) desired.Add(mapping.Key);
        }
        foreach (int key in held.Except(desired).ToArray())
            if (Native.SendKey(key, false)) held.Remove(key);
        foreach (int key in desired.Except(held))
        {
            if (Native.SendKey(key, true)) held.Add(key);
            else Report("Windows blocked keyboard output. Check the app and mapper privilege levels.");
        }
    }

    void InterruptOutput()
    {
        foreach (var mapping in settings.Maps)
            if (active.Contains(mapping.Source)) blockedUntilReleased.Add(mapping.Source);
        ReleaseAll();
    }

    void ReleaseAll()
    {
        foreach (int key in held.ToArray())
            if (Native.SendKey(key, false)) held.Remove(key);
    }

    void StartListening(int row)
    {
        CancelListening();
        InterruptOutput();
        listening = row;
        listenDeadline = Environment.TickCount64 + 15000;
        listenButtons[row].Text = "Cancel";
        Report($"Press a keyboard or numpad key for {settings.Maps[row].Name}.");
        Activate();
        listenButtons[row].Focus();
    }

    void CancelListening()
    {
        if (listening >= 0)
        {
            listenButtons[listening].Text = "Listen";
            listening = -1;
            Report("Keyboard listening stopped.");
        }
        capturedVk = -1;
    }

    static int CapturedKey(int vk, long data)
    {
        int scan = (int)((data >> 16) & 255);
        bool extended = (data & (1L << 24)) != 0;
        if (vk == (int)Keys.Enter && extended) return 0x1000;
        if (vk == (int)Keys.ShiftKey) return (int)(scan == 0x36 ? Keys.RShiftKey : Keys.LShiftKey);
        if (vk == (int)Keys.ControlKey) return (int)(extended ? Keys.RControlKey : Keys.LControlKey);
        if (vk == (int)Keys.Menu) return (int)(extended ? Keys.RMenu : Keys.LMenu);
        if (!extended)
        {
            int num = scan switch
            {
                0x52 => 0, 0x4f => 1, 0x50 => 2, 0x51 => 3, 0x4b => 4,
                0x4c => 5, 0x4d => 6, 0x47 => 7, 0x48 => 8, 0x49 => 9, _ => -1
            };
            if (num >= 0) return (int)Keys.NumPad0 + num;
            if (scan == 0x53) return (int)Keys.Decimal;
        }
        return vk;
    }

    public bool PreFilterMessage(ref Message message)
    {
        bool down = message.Msg is 0x100 or 0x104;
        bool up = message.Msg is 0x101 or 0x105;
        if (!down && !up) return false;
        int vk = (int)message.WParam;
        if (capturedVk >= 0)
        {
            if (vk != capturedVk) return false;
            if (up) capturedVk = -1;
            return true;
        }
        if (listening < 0) return false;
        if (!ContainsFocus && !loading)
        {
            CancelListening();
            return false;
        }
        if (up) return true;
        int row = listening;
        int code = CapturedKey(vk, message.LParam.ToInt64());
        if (code <= 0 || code > 0x1000) return true;
        var box = keyBoxes[row];
        var key = box.Items.OfType<KeyChoice>().FirstOrDefault(item => item.Code == code);
        if (key is null) { key = new KeyChoice(code); box.Items.Add(key); }
        CancelListening();
        capturedVk = vk;
        box.SelectedItem = key;
        Report($"{settings.Maps[row].Name} will send {key}.");
        return true;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x808d)
        {
            ExitMapper();
            return;
        }
        if (hardware && message.Msg == 0xff)
        {
            try
            {
                var input = Native.Read(message.LParam);
                if (input is { } report) UpdateSource(report.prefix, report.buttons);
            }
            catch (Exception) { Report("A HID report could not be decoded."); }
        }
        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Application.RemoveMessageFilter(this);
            pollTimer.Dispose();
            ReleaseAll();
            vendor?.Dispose();
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
        }
        base.Dispose(disposing);
    }

    public static string SelfTest()
    {
        var results = new List<string>();
        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("FAIL " + name);
            results.Add("PASS " + name);
        }

        using var mapper = new Mapper(false, false) { loading = true };
        Check(Native.AbiValid(), "Windows input structure sizes");
        Check(Native.ScanCode(0x1000) == 0xE01C && Native.ScanCode((int)Keys.Enter) == 0x1C,
            "Numpad Enter remains distinct from Enter");
        Check(Native.ScanCode((int)Keys.NumPad0) != Native.ScanCode((int)Keys.Insert)
            && Native.ScanCode((int)Keys.NumPad0) != 0, "Numpad zero remains distinct from Insert");
        Check(mapper.settings.Maps.Count == 6, "Six mapping slots");
        Check(StartupTask.Definition(@"C:\Test & Space\mapper.exe", "S-1-5-21-123").Contains("HighestAvailable")
            && System.Xml.Linq.XDocument.Parse(StartupTask.Definition(@"C:\Test & Space\mapper.exe", "S-1-5-21-123"))
                .Descendants().Any(x => x.Name.LocalName == "Command" && x.Value == @"C:\Test & Space\mapper.exe"),
            "Startup task preserves the executable path and elevation");
        Check(CapturedKey(13, (0x1cL << 16) | (1L << 24)) == 0x1000 && CapturedKey(13, 0x1cL << 16) == 13,
            "Listening distinguishes numpad Enter");
        Check(CapturedKey((int)Keys.Insert, 0x52L << 16) == (int)Keys.NumPad0
            && CapturedKey((int)Keys.Insert, (0x52L << 16) | (1L << 24)) == (int)Keys.Insert,
            "Listening distinguishes numpad with Num Lock off");
        Check(CapturedKey((int)Keys.ControlKey, (0x1dL << 16) | (1L << 24)) == (int)Keys.RControlKey,
            "Listening preserves right Control");
        mapper.StartListening(0);
        var key = Message.Create(mapper.Handle, 0x100, (IntPtr)Keys.F9, (IntPtr)(0x43L << 16));
        Check(mapper.PreFilterMessage(ref key) && mapper.settings.Maps[0].Key == (int)Keys.F9 && mapper.listening < 0,
            "Listening saves the next key and stops");
        var release = Message.Create(mapper.Handle, 0x101, (IntPtr)Keys.F9, IntPtr.Zero);
        Check(mapper.PreFilterMessage(ref release) && mapper.capturedVk < 0, "Captured release is consumed");

        string file = AppPaths.FilePath("settings-test.json");
        File.WriteAllText(file, """{"GameOnly":true,"Maps":[{"Name":"C","Source":"V:10","Key":98}]}""");
        var legacy = Settings.Load(file);
        Check(legacy.Maps[0].Key == 98 && legacy.Maps.Count == 6 && legacy.EffectiveScope == AppScope.LegacyGames,
            "Legacy GTA preference and mappings migrate");
        var game = new ForegroundApp(IntPtr.Zero, 12, "FiveM_b3095_GTAProcess.exe", @"C:\Games\FiveM_b3095_GTAProcess.exe");
        var note = new ForegroundApp(IntPtr.Zero, 20, "notepad.exe", @"C:\Windows\notepad.exe");
        Check(AppRouting.Allows(legacy.EffectiveScope, null, game.Executable)
            && !AppRouting.Allows(legacy.EffectiveScope, null, note.Executable), "Legacy game matching");
        legacy.Scope = AppScope.ListedApps;
        legacy.AppRules.Add(new AppRule { Target = "NOTEPAD.EXE", Action = AppAction.Enable });
        legacy.AppRules.Add(new AppRule { Target = @"C:\Windows\notepad.exe", Action = AppAction.Pause });
        Check(AppRouting.Allows(AppScope.ListedApps, AppRouting.Match(legacy.AppRules, note with { Path = "" }), note.Executable)
            && !AppRouting.Allows(AppScope.ListedApps, AppRouting.Match(legacy.AppRules, note), note.Executable),
            "Exact path takes precedence over a case-insensitive executable name");
        Check(!AppRouting.Allows(AppScope.ListedApps, null, game.Executable)
            && AppRouting.Allows(AppScope.Everywhere, null, game.Executable), "Both app scope defaults");
        Check(AppRouting.NormalizeTarget(" notepad ") == "notepad.exe"
            && AppRouting.NormalizeTarget(@"Games\test.exe") == "" && AppRouting.NormalizeTarget("*.exe") == "",
            "App rule target validation");
        legacy.MappingEnabled = false;
        legacy.CloseToTray = false;
        legacy.Appearance = Appearance.Light;
        legacy.Accent = Accent.Violet;
        legacy.Save(file);
        var reloaded = Settings.Load(file);
        Check(reloaded.Maps[0].Key == 98 && !reloaded.MappingEnabled && !reloaded.CloseToTray
            && reloaded.Appearance == Appearance.Light && reloaded.Accent == Accent.Violet
            && reloaded.EffectiveScope == AppScope.ListedApps && reloaded.AppRules.Count == 2,
            "Settings and app rules round-trip without losing the legacy mapping");
        mapper.settings.LogButtons = false;
        mapper.UpdateSource("V:", ["V:10"]);
        mapper.ApplyMappings(false);
        Check(mapper.blockedUntilReleased.Contains("V:10"), "Presses while paused wait for release");
        mapper.ApplyMappings(true);
        Check(mapper.blockedUntilReleased.Contains("V:10") && mapper.held.Count == 0,
            "Resuming does not inject a held input");
        mapper.UpdateSource("V:", []);
        mapper.ApplyMappings(true);
        Check(mapper.blockedUntilReleased.Count == 0, "Physical release re-arms the input");
        return string.Join(Environment.NewLine, results);
    }
}

sealed record KeyChoice(int Code)
{
    public override string ToString() => Code switch
    {
        0 => "Unassigned",
        0x1000 => "Numpad Enter",
        >= 96 and <= 105 => $"Numpad {Code - 96}",
        >= 48 and <= 57 => ((char)Code).ToString(),
        (int)Keys.Add => "Numpad +", (int)Keys.Subtract => "Numpad −",
        (int)Keys.Multiply => "Numpad ×", (int)Keys.Divide => "Numpad ÷",
        (int)Keys.Decimal => "Numpad .", (int)Keys.Back => "Backspace",
        (int)Keys.LControlKey => "Left Ctrl", (int)Keys.RControlKey => "Right Ctrl",
        (int)Keys.LShiftKey => "Left Shift", (int)Keys.RShiftKey => "Right Shift",
        (int)Keys.LMenu => "Left Alt", (int)Keys.RMenu => "Right Alt",
        _ => ((Keys)Code).ToString()
    };
}
