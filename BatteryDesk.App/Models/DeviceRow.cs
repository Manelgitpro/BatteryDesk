using BatteryDesk.Devices.HID;

namespace BatteryDesk;

public sealed class DeviceRow
{
    public string Name { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string HardwareId { get; init; } = "";
    public string Serial { get; init; } = "";
    public string Status { get; init; } = "";

    public static DeviceRow From(HidDeviceInfo device) => new()
    {
        Name = device.DisplayName,
        Manufacturer = string.IsNullOrWhiteSpace(device.Manufacturer)
            ? "Fabricante não informado"
            : device.Manufacturer,
        HardwareId = device.HardwareId,
        Serial = string.IsNullOrWhiteSpace(device.SerialNumber)
            ? "Número de série não informado"
            : $"S/N: {device.SerialNumber}",
        Status = "HID detectado · bateria ainda não consultada"
    };
}
