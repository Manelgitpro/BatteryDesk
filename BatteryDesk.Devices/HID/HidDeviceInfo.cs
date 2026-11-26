namespace BatteryDesk.Devices.HID;

public sealed record HidDeviceInfo(
    string DevicePath,
    ushort VendorId,
    ushort ProductId,
    ushort VersionNumber,
    string Manufacturer,
    string Product,
    string SerialNumber)
{
    public string HardwareId => $"VID_{VendorId:X4}&PID_{ProductId:X4}";
    public string DisplayName => string.IsNullOrWhiteSpace(Product)
        ? "Dispositivo HID sem nome"
        : Product;
}
