using System.Text.Json;

namespace FlydigiKeyMapper;

enum AppScope { Everywhere, ListedApps, LegacyGames }
enum AppAction { Enable, Pause }
enum Appearance { Midnight, Slate, Light }
enum Accent { Mint, Blue, Violet, Amber }

sealed class Mapping
{
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public int Key { get; set; }
}

sealed class AppRule
{
    public string Target { get; set; } = "";
    public AppAction Action { get; set; }
}

sealed class Settings
{
    static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public bool GameOnly { get; set; }
    public List<Mapping> Maps { get; set; } = [];
    public bool MappingEnabled { get; set; } = true;
    public AppScope? Scope { get; set; }
    public List<AppRule> AppRules { get; set; } = [];
    public Appearance Appearance { get; set; }
    public Accent Accent { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool TrayNotice { get; set; } = true;
    public bool LogButtons { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public AppScope EffectiveScope => Scope ?? (GameOnly ? AppScope.LegacyGames : AppScope.Everywhere);

    public static Settings Load(string file)
    {
        var settings = File.Exists(file)
            ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(file)) ?? new()
            : new Settings();
        settings.Normalize();
        return settings;
    }

    internal void Normalize()
    {
        var oldMaps = Maps ?? [];
        Maps = VendorInput.Names.Select((name, index) => new Mapping
        {
            Name = name,
            Source = $"V:{VendorInput.Codes[index]:X2}",
            Key = oldMaps.FirstOrDefault(map => map?.Name == name)?.Key
                ?? (oldMaps.Count == VendorInput.Names.Length ? oldMaps[index]?.Key : null)
                ?? 0
        }).ToList();
        AppRules = (AppRules ?? []).Where(rule => rule is not null && Enum.IsDefined(rule.Action))
            .Select(rule => new AppRule { Target = AppRouting.NormalizeTarget(rule.Target), Action = rule.Action })
            .Where(rule => rule.Target.Length > 0)
            .DistinctBy(rule => rule.Target, StringComparer.OrdinalIgnoreCase).ToList();
        if (Scope is AppScope scope && !Enum.IsDefined(scope)) Scope = null;
        if (!Enum.IsDefined(Appearance)) Appearance = Appearance.Midnight;
        if (!Enum.IsDefined(Accent)) Accent = Accent.Mint;
    }

    public void Save(string file)
    {
        var temporary = file + ".tmp";
        GameOnly = EffectiveScope == AppScope.LegacyGames;
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, Format));
            File.Move(temporary, file, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
