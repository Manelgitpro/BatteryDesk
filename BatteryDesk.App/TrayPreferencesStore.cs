using System.IO;
using System.Text.Json;

namespace BatteryDesk;

internal sealed class TrayPreferences
{
    public bool ShowDeluxMouse { get; set; }
    public bool ShowZeusHeadset { get; set; }
    public int? LastDeluxBatteryPercent { get; set; }
    public DateTimeOffset? LastDeluxReadingAt { get; set; }
}

internal sealed class TrayPreferencesStore
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BatteryDesk",
        "TrayPreferences.json");

    public TrayPreferences Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new TrayPreferences();

            return JsonSerializer.Deserialize<TrayPreferences>(File.ReadAllText(_settingsPath))
                   ?? new TrayPreferences();
        }
        catch
        {
            return new TrayPreferences();
        }
    }

    public void Save(TrayPreferences preferences)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Directory.CreateDirectory(directory);
            var temporaryPath = _settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        catch
        {
            // Tray preferences are best-effort; persistence must not affect the devices.
        }
    }
}
