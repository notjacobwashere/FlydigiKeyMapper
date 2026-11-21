using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FlydigiKeyMapper;

readonly record struct ForegroundApp(IntPtr Window, uint ProcessId, string Executable, string Path)
{
    public string DisplayName => Executable.Length == 0 ? "No app detected" : Executable;
}

static class AppRouting
{
    public static string NormalizeTarget(string? value)
    {
        var target = (value ?? "").Trim().Trim('"').Trim();
        if (target.Length == 0 || target.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0)
            return "";
        if (target.Contains('\\') || target.Contains('/') || target.Contains(':'))
        {
            if (!Path.IsPathFullyQualified(target)) return "";
            try { target = Path.GetFullPath(target); }
            catch (ArgumentException) { return ""; }
            catch (NotSupportedException) { return ""; }
            catch (PathTooLongException) { return ""; }
            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return "";
        }
        else
        {
            if (target.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "";
            if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) target += ".exe";
        }
        return Path.GetFileNameWithoutExtension(target).Length == 0 ? "" : target;
    }

    public static AppRule? Match(IReadOnlyList<AppRule> rules, ForegroundApp app)
    {
        AppRule? nameRule = null;
        foreach (var rule in rules)
        {
            if (app.Path.Length > 0 && rule.Target.Equals(app.Path, StringComparison.OrdinalIgnoreCase))
                return rule;
            if (rule.Target.Equals(app.Executable, StringComparison.OrdinalIgnoreCase))
                nameRule = rule;
        }
        return nameRule;
    }

    public static bool Allows(AppScope scope, AppRule? rule, string executable)
    {
        if (rule is not null) return rule.Action == AppAction.Enable;
        if (scope == AppScope.Everywhere) return true;
        if (scope != AppScope.LegacyGames) return false;
        var name = Path.GetFileNameWithoutExtension(executable);
        return name.Equals("GTA5", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("GTA5_", StringComparison.OrdinalIgnoreCase)
            || name.Equals("GTAIV", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("FiveM", StringComparison.OrdinalIgnoreCase);
    }
}

sealed class ForegroundTracker
{
    ForegroundApp current = new(IntPtr.Zero, 0, "", "");

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    static extern IntPtr GetWindow(IntPtr window, uint command);
    delegate bool WindowVisitor(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    static extern bool EnumWindows(WindowVisitor visitor, IntPtr parameter);
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int length);
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);

    public ForegroundApp Read()
    {
        var window = GetForegroundWindow();
        uint id = 0;
        if (window != IntPtr.Zero) GetWindowThreadProcessId(window, out id);
        if (current.Window != window || current.ProcessId != id) current = Identify(window, id);
        return current;
    }

    public static List<(string Title, ForegroundApp App)> OpenApps()
    {
        var result = new List<(string Title, ForegroundApp App)>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out uint id);
            if (id == 0 || id == Environment.ProcessId) return true;
            var title = new StringBuilder(256);
            if (GetWindowText(window, title, title.Capacity) == 0) return true;
            var app = Identify(window, id);
            if (app.Executable.Length > 0) result.Add((title.ToString(), app));
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(item => item.App.Executable, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(item => item.App.Path.Length > 0 ? item.App.Path : item.App.Executable, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static ForegroundApp Identify(IntPtr window, uint id)
    {
        // Store apps can be hosted in ApplicationFrameHost; their own window is a child.
        string path = ImagePath(id);
        string executable = FileName(path, id);
        if (executable.Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            for (var child = GetWindow(window, 5); child != IntPtr.Zero; child = GetWindow(child, 2))
            {
                GetWindowThreadProcessId(child, out uint childId);
                if (childId == 0 || childId == id) continue;
                string childPath = ImagePath(childId);
                string childExe = FileName(childPath, childId);
                if (childExe.Length > 0) return new(window, id, childExe, childPath);
            }
        }
        return new(window, id, executable, path);
    }

    static string FileName(string path, uint id)
    {
        if (path.Length > 0) return Path.GetFileName(path);
        if (id == 0) return "";
        try
        {
            using var process = Process.GetProcessById((int)id);
            return process.ProcessName + ".exe";
        }
        catch (ArgumentException) { return ""; }
        catch (InvalidOperationException) { return ""; }
        catch (System.ComponentModel.Win32Exception) { return ""; }
    }

    static string ImagePath(uint id)
    {
        if (id == 0) return "";
        var handle = OpenProcess(0x1000, false, id);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var path = new StringBuilder(32768);
            int length = path.Capacity;
            return QueryFullProcessImageName(handle, 0, path, ref length) ? path.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }
}
