using System.IO;
using System.Text.Json;

namespace BatteryDesk;

/// <summary>
/// Keeps a local, time-based estimate for the H510-PRO charging session.
/// It only persists timestamps; it never communicates with the headset.
/// </summary>
internal sealed class ZeusChargeSession
{
    public static readonly TimeSpan EstimatedFullChargeTime = TimeSpan.FromHours(3);

    private readonly string _statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BatteryDesk",
        "ZeusProChargeSession.json");

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public bool IsUsbConnected { get; private set; }
    public bool WasInterruptedWhileAppClosed { get; private set; }
    public bool StartedAtAppLaunch { get; private set; }

    public ZeusChargeSession()
    {
        Load();
    }

    public void Initialize(bool usbConnected, DateTimeOffset now)
    {
        IsUsbConnected = usbConnected;

        if (usbConnected)
        {
            if (!StartedAt.HasValue || EndedAt.HasValue || WasInterruptedWhileAppClosed)
            {
                Start(now);
                StartedAtAppLaunch = true;
            }
        }
        else if (StartedAt.HasValue && !EndedAt.HasValue)
        {
            // The app cannot know when the cable was removed while it was closed.
            WasInterruptedWhileAppClosed = true;
            Save();
        }
    }

    public void SetUsbConnected(bool usbConnected, DateTimeOffset now)
    {
        if (IsUsbConnected == usbConnected)
            return;

        IsUsbConnected = usbConnected;
        StartedAtAppLaunch = false;

        if (usbConnected)
        {
            Start(now);
        }
        else if (StartedAt.HasValue && !EndedAt.HasValue)
        {
            EndedAt = now;
            WasInterruptedWhileAppClosed = false;
            Save();
        }
    }

    public void Reset(DateTimeOffset now)
    {
        StartedAtAppLaunch = false;

        if (IsUsbConnected)
        {
            Start(now);
        }
        else
        {
            StartedAt = null;
            EndedAt = null;
            WasInterruptedWhileAppClosed = false;
            Save();
        }
    }

    private void Start(DateTimeOffset now)
    {
        StartedAt = now;
        EndedAt = null;
        WasInterruptedWhileAppClosed = false;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_statePath))
                return;

            var saved = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(_statePath));
            if (saved is null)
                return;

            StartedAt = saved.StartedAt;
            EndedAt = saved.EndedAt;
            WasInterruptedWhileAppClosed = saved.WasInterruptedWhileAppClosed;
        }
        catch
        {
            // A damaged or inaccessible local state file must not prevent the app from opening.
            StartedAt = null;
            EndedAt = null;
            WasInterruptedWhileAppClosed = false;
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_statePath);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Directory.CreateDirectory(directory);
            var tempPath = _statePath + ".tmp";
            var state = new PersistedState(StartedAt, EndedAt, WasInterruptedWhileAppClosed);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(state));
            File.Move(tempPath, _statePath, overwrite: true);
        }
        catch
        {
            // Local persistence is best-effort and never affects device communication.
        }
    }

    private sealed record PersistedState(
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt,
        bool WasInterruptedWhileAppClosed);
}
