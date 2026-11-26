using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace BatteryDesk.Devices.HID;

/// <summary>
/// Enumera interfaces HID presentes e lê descritores disponíveis.
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
                // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize is 8 on x64 and 6 on x86.
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

                devices.Add(new HidDeviceInfo(
                    devicePath,
                    attributes.VendorId,
                    attributes.ProductId,
                    attributes.VersionNumber,
                    ReadString(handle, HidD_GetManufacturerString),
                    ReadString(handle, HidD_GetProductString),
                    ReadString(handle, HidD_GetSerialNumberString)));
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
            .ToArray();
    }

    private delegate byte HidStringReader(SafeFileHandle handle, byte[] buffer, int bufferLength);

    private static string ReadString(SafeFileHandle handle, HidStringReader reader)
    {
        var buffer = new byte[512];
        return reader(handle, buffer, buffer.Length) == 0
            ? string.Empty
            : Encoding.Unicode.GetString(buffer).TrimEnd('\0').Trim();
    }

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

    public sealed class SafeDeviceInfoSet : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeDeviceInfoSet() : base(true) { }
        protected override bool ReleaseHandle() => SetupDiDestroyDeviceInfoList(handle);
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    // HID API functions below return BOOLEAN (one byte), not Win32 BOOL.
    [DllImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
    private static extern byte HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);

    [DllImport("hid.dll", EntryPoint = "HidD_GetManufacturerString")]
    private static extern byte HidD_GetManufacturerString(SafeFileHandle handle, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetProductString")]
    private static extern byte HidD_GetProductString(SafeFileHandle handle, byte[] buffer, int bufferLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetSerialNumberString")]
    private static extern byte HidD_GetSerialNumberString(SafeFileHandle handle, byte[] buffer, int bufferLength);

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
