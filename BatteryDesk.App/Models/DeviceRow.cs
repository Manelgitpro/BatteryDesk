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
    public string TechnicalDetails { get; init; } = "";

    public static DeviceRow From(
        HidDeviceInfo device,
        int interfaceCount = 1,
        DeluxM800BatteryResult? deluxReading = null,
        bool isDeluxReceiver = false,
        bool deluxQueryAuthorized = false)
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
        else if (isSupportedReceiver && isDeluxReceiver && !deluxQueryAuthorized)
        {
            batteryText = "Consulta não autorizada";
            status = "Nenhum comando foi enviado ao receptor";
        }
        else if (isSupportedReceiver && isDeluxReceiver)
        {
            batteryText = "Aguardando resposta de bateria";
            status = "Consulta autorizada · aguardando resposta";
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
            InterfaceText = interfaceCount > 1 ? $"{interfaceCount} interfaces HID agrupadas" : device.InterfaceLabel,
            TechnicalDetails = FormatInterfaceDetails(device)
        };
    }

    public static DeviceRow FromKeyboardGroup(IReadOnlyList<HidDeviceInfo> interfaces)
    {
        var first = interfaces[0];
        var summaries = interfaces
            .GroupBy(d => d.CollectionType)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToArray();

        var details = interfaces
            .OrderBy(d => d.InterfaceNumber)
            .ThenBy(d => d.CollectionNumber)
            .Select(d => $"{d.InterfaceLabel} · {d.CollectionType} · Uso {d.UsagePage:X4}:{d.Usage:X4} · {d.ReportSummary}")
            .ToArray();

        return new DeviceRow
        {
            Name = "USB Gaming Keyboard",
            Manufacturer = string.IsNullOrWhiteSpace(first.Manufacturer)
                ? "SEMICO"
                : first.Manufacturer,
            HardwareId = first.HardwareId,
            Serial = string.IsNullOrWhiteSpace(first.SerialNumber)
                ? "Número de série não informado"
                : $"S/N: {first.SerialNumber}",
            BatteryText = "Teclado USB · sem bateria monitorada",
            Status = "Varredura HID detalhada concluída",
            InterfaceText = $"{interfaces.Count} coleções/interfaces HID agrupadas",
            TechnicalDetails = summaries.Length == 0
                ? string.Join(Environment.NewLine, details)
                : string.Join(" · ", summaries) + Environment.NewLine + string.Join(Environment.NewLine, details)
        };
    }

    private static string FormatInterfaceDetails(HidDeviceInfo device) =>
        $"{device.InterfaceLabel} · {device.CollectionType} · Uso {device.UsagePage:X4}:{device.Usage:X4} · {device.ReportSummary}";
}
