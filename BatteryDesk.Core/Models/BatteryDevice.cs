namespace BatteryDesk.Core.Models;

public enum DeviceConnectionType { Usb, Bluetooth, Wired, Unknown }
public enum BatteryReadingStatus { Available, Unsupported, NotDetected, Demo }

public sealed record BatteryDevice(
    string Id,
    string Name,
    string Category,
    DeviceConnectionType Connection,
    int? BatteryPercent,
    BatteryReadingStatus ReadingStatus,
    DateTimeOffset? LastUpdated = null,
    string? Details = null);
