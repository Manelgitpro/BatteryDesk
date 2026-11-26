using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace BatteryDesk.Devices.HID;

/// <summary>
/// Enumera interfaces HID presentes e consulta somente descritores e capacidades.
/// Não envia Output Reports nem Feature Reports.
/// </summary>
public sealed class HidEnumerator
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint OpenExisting = 3;
    private const uint ShareRead = 1;
    private const uint ShareWrite = 2;
    private const uint FileAttributeNormal = 0x00000080;
    private const int ErrorNoMoreItems = 259;
    private const int HidpStatusSuccess = 0x00110000;

    public IReadOnlyList<HidDeviceInfo> Enumerate()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A descoberta HID está disponível somente no Windows.");

        HidD_GetHidGuid(out var hidGuid);
        using var deviceInfoSet = SetupDiGetClassDevs(
            ref hidGuid, null, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);

        if (deviceInfoSet.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Falha ao enumerar interfaces HID.");

        var devices = new List<HidDeviceInfo>();

        for (uint index = 0; ; index++)
        {
            var interfaceData = new SpDeviceInterfaceData
            {
                CbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
            };

            if (!SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ErrorNoMoreItems)
                    break;

                continue;
            }

            _ = SetupDiGetDeviceInterfaceDetail(
                deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero);

            if (requiredSize < 6 || requiredSize > int.MaxValue)
                continue;

            var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
            try
            {
                Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                if (!SetupDiGetDeviceInterfaceDetail(
                    deviceInfoSet, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                    continue;

                var devicePath = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                if (string.IsNullOrWhiteSpace(devicePath))
                    continue;

                using var handle = CreateFile(
                    devicePath, 0, ShareRead | ShareWrite, IntPtr.Zero,
                    OpenExisting, FileAttributeNormal, IntPtr.Zero);

                if (handle.IsInvalid)
                    continue;

                var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
                if (HidD_GetAttributes(handle, ref attributes) == 0)
                    continue;

                var caps = ReadCapabilities(handle);
                var (interfaceNumber, collectionNumber) = ParsePathIndexes(devicePath);

                devices.Add(new HidDeviceInfo(
                    devicePath,
                    attributes.VendorId,
                    attributes.ProductId,
                    attributes.VersionNumber,
                    ReadString(handle, HidD_GetManufacturerString),
                    ReadString(handle, HidD_GetProductString),
                    ReadString(handle, HidD_GetSerialNumberString),
                    caps?.UsagePage ?? 0,
                    caps?.Usage ?? 0,
                    caps?.InputReportByteLength ?? 0,
                    caps?.OutputReportByteLength ?? 0,
                    caps?.FeatureReportByteLength ?? 0,
                    interfaceNumber,
                    collectionNumber,
                    caps is not null));
            }
            finally
            {
                Marshal.FreeHGlobal(detailBuffer);
            }
        }

        return devices
            .GroupBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(device => device.Manufacturer)
            .ThenBy(device => device.Product)
            .ThenBy(device => device.InterfaceNumber)
            .ThenBy(device => device.CollectionNumber)
            .ToArray();
    }

    private static HidCapabilities? ReadCapabilities(SafeFileHandle handle)
    {
        if (HidD_GetPreparsedData(handle, out var preparsedData) == 0 ||
            preparsedData == IntPtr.Zero)
            return null;

        try
        {
            var caps = new HidpCaps { Reserved = new ushort[17] };
            if (HidP_GetCaps(preparsedData, ref caps) != HidpStatusSuccess)
                return null;

            return new HidCapabilities(
                caps.UsagePage,
                caps.Usage,
                caps.InputReportByteLength,
                caps.OutputReportByteLength,
                caps.FeatureReportByteLength);
        }
        finally
        {
            _ = HidD_FreePreparsedData(preparsedData);
        }
    }

    private static (int? InterfaceNumber, int? CollectionNumber) ParsePathIndexes(string path)
    {
        var interfaceMatch = Regex.Match(path, @"&mi_([0-9a-f]{2})", RegexOptions.IgnoreCase);
        var collectionMatch = Regex.Match(path, @"&col([0-9a-f]{2})", RegexOptions.IgnoreCase);

        int? interfaceNumber = interfaceMatch.Success
            ? Convert.ToInt32(interfaceMatch.Groups[1].Value, 16)
            : null;
        int? collectionNumber = collectionMatch.Success
            ? Convert.ToInt32(collectionMatch.Groups[1].Value, 16)
            : null;

        return (interfaceNumber, collectionNumber);
    }

    private delegate byte HidStringReader(SafeFileHandle handle, byte[] buffer, int bufferLength);

    private static string ReadString(SafeFileHandle handle, HidStringReader reader)
    {
        var buffer = new byte[512];
        return reader(handle, buffer, buffer.Length) == 0
            ? string.Empty
            : Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
    }

    private sealed record HidCapabilities(
        ushort UsagePage,
        ushort Usage,
        ushort InputReportByteLength,
        ushort OutputReportByteLength,
        ushort FeatureReportByteLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int CbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    public sealed class SafeDeviceInfoSet : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeDeviceInfoSet() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
    private static extern byte HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);

    [DllImport("hid.dll", EntryPoint = "HidD_GetManufacturerString")]
    private static extern byte HidD_GetManufacturerString(SafeFileHandle handle, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetProductString")]
    private static extern byte HidD_GetProductString(SafeFileHandle handle, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetSerialNumberString")]
    private static extern byte HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetPreparsedData")]
    private static extern byte HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsedData);

    [DllImport("hid.dll", EntryPoint = "HidD_FreePreparsedData")]
    private static extern byte HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", EntryPoint = "HidP_GetCaps")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, ref HidpCaps capabilities);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeDeviceInfoSet SetupDiGetClassDevs(
        ref Guid classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        SafeDeviceInfoSet deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid,
        uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        SafeDeviceInfoSet deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr detailData, uint detailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
}
