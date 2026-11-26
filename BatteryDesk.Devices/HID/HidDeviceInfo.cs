using System.Text.RegularExpressions;

namespace BatteryDesk.Devices.HID;

public sealed record HidDeviceInfo(
    string DevicePath,
    ushort VendorId,
    ushort ProductId,
    ushort VersionNumber,
    string Manufacturer,
    string Product,
    string SerialNumber,
    ushort UsagePage = 0,
    ushort Usage = 0,
    ushort InputReportByteLength = 0,
    ushort OutputReportByteLength = 0,
    ushort FeatureReportByteLength = 0,
    int? InterfaceNumber = null,
    int? CollectionNumber = null,
    bool HasHidCapabilities = false)
{
    public string HardwareId => $"VID_{VendorId:X4}&PID_{ProductId:X4}";

    public string DisplayName => string.IsNullOrWhiteSpace(Product)
        ? "Dispositivo HID sem nome"
        : Product;

    public string CollectionType => (UsagePage, Usage) switch
    {
        (0x0001, 0x0006) => "Teclado",
        (0x0001, 0x0002) => "Mouse",
        (0x0001, 0x0080) => "Controle do sistema",
        (0x000C, _) => "Controles multimídia",
        (>= 0xFF00, _) => "Coleção proprietária do fabricante",
        (0x0001, _) => "Dispositivo de uso geral",
        (0, 0) when !HasHidCapabilities => "Descritor HID indisponível",
        _ => "Outra coleção HID"
    };

    public string InterfaceLabel
    {
        get
        {
            var parts = new List<string>();
            if (InterfaceNumber is int mi) parts.Add($"MI_{mi:X2}");
            if (CollectionNumber is int col) parts.Add($"COL_{col:X2}");
            return parts.Count == 0 ? "Interface sem índice" : string.Join(" / ", parts);
        }
    }

    public string ReportSummary => HasHidCapabilities
        ? $"IN {InputReportByteLength} B · OUT {OutputReportByteLength} B · FEATURE {FeatureReportByteLength} B"
        : "Tamanhos dos relatórios não disponíveis";

    /// <summary>
    /// Agrupa as coleções HID pertencentes à mesma instância do dispositivo,
    /// removendo o índice de coleção e o sufixo de coleção do caminho HID.
    /// </summary>
    public string PhysicalGroupKey
    {
        get
        {
            var path = DevicePath.ToLowerInvariant();
            var guidStart = path.IndexOf("#{", StringComparison.Ordinal);
            if (guidStart >= 0) path = path[..guidStart];

            var lastHash = path.LastIndexOf('#');
            if (lastHash >= 0 && lastHash < path.Length - 1)
            {
                var prefix = path[..(lastHash + 1)];
                var suffix = path[(lastHash + 1)..];
                suffix = Regex.Replace(suffix, @"&[0-9a-f]{4}$", "");
                path = prefix + suffix;
            }

            path = Regex.Replace(path, @"&col[0-9a-f]{2}", "");
            return $"{VendorId:X4}:{ProductId:X4}:{path}";
        }
    }
}
