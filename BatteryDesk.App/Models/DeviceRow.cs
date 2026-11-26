using BatteryDesk.Devices.HID;

namespace BatteryDesk;

public sealed class DeviceRow
{
    public string Name { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string HardwareId { get; init; } = "";
    public string Serial { get; init; } = "";
    public string Status { get; init; } = "";
    public string BatteryText { get; init; } = "";
    public string InterfaceText { get; init; } = "";

    public static DeviceRow From(
        HidDeviceInfo device,
        int interfaceCount = 1,
        DeluxM800BatteryResult? deluxReading = null,
        bool isDeluxReceiver = false)
    {
        var isSupportedReceiver = device.VendorId == 0x248A && device.ProductId == 0x5B2F;
        var displayName = isSupportedReceiver ? "Delux M800 Pro" : device.DisplayName;

        string batteryText;
        string status;

        if (isSupportedReceiver && deluxReading?.Percent is int percent)
        {
            batteryText = $"{percent}% · {(deluxReading.IsFullyCharged ? "Carga completa" : deluxReading.IsCharging ? "Carregando" : "Descarregando")}";
            status = "Bateria consultada pelo receptor 2,4 GHz";
        }
        else if (isSupportedReceiver && deluxReading is { IsConnected: false })
        {
            batteryText = "Mouse não conectado ao receptor";
            status = "Receptor localizado · mouse não conectado";
        }
        else if (isSupportedReceiver && isDeluxReceiver)
        {
            batteryText = "Aguardando resposta de bateria";
            status = "Receptor identificado · consulta não respondeu";
        }
        else
        {
            batteryText = "Bateria ainda não consultada";
            status = "Interface HID detectada";
        }

        return new DeviceRow
        {
            Name = displayName,
            Manufacturer = isSupportedReceiver
                ? "Receptor Delux · conexão 2,4 GHz"
                : string.IsNullOrWhiteSpace(device.Manufacturer)
                    ? "Fabricante não informado"
                    : device.Manufacturer,
            HardwareId = device.HardwareId,
            Serial = string.IsNullOrWhiteSpace(device.SerialNumber)
                ? "Número de série não informado"
                : $"S/N: {device.SerialNumber}",
            Status = status,
            BatteryText = batteryText,
            InterfaceText = interfaceCount > 1 ? $"{interfaceCount} interfaces HID agrupadas" : "HID"
        };
    }
}
