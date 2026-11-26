using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace BatteryDesk.Devices.HID;

public sealed record DeluxM800BatteryResult(
    bool IsConnected,
    int? Percent,
    bool IsCharging,
    bool IsFullyCharged,
    string ModelCode);

/// <summary>
/// Consulta somente o receptor Delux M800 Pro 2,4 GHz conhecido (VID 248A, PID 5B2F).
/// A consulta usa Feature Report 0C no uso HID FF00:0001; não altera configurações.
/// </summary>
public sealed class DeluxM800BatteryReader
{
    private const ushort VendorId = 0x248A;
    private const ushort ReceiverProductId = 0x5B2F;
    private const ushort UsagePage = 0xFF00;
    private const ushort Usage = 0x0001;
    private const int HidpStatusSuccess = 0x00110000;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private static int _sequence;

    public DeluxM800BatteryResult? Read(IReadOnlyList<HidDeviceInfo> devices)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A leitura HID está disponível somente no Windows.");

        foreach (var device in devices.Where(d =>
                     d.VendorId == VendorId && d.ProductId == ReceiverProductId))
        {
            using var handle = CreateFile(
                device.DevicePath,
                GenericRead | GenericWrite,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileAttributeNormal,
                IntPtr.Zero);

            if (handle.IsInvalid)
                continue;

            var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
            if (HidD_GetAttributes(handle, ref attributes) == 0 ||
                attributes.VendorId != VendorId ||
                attributes.ProductId != ReceiverProductId)
                continue;

            if (HidD_GetPreparsedData(handle, out var preparsedData) == 0)
                continue;

            try
            {
                var caps = new HidpCaps { Reserved = new ushort[17] };
                var status = HidP_GetCaps(preparsedData, ref caps);

                // HidP_GetCaps reports HIDP_STATUS_SUCCESS (0x00110000).
                if (status != HidpStatusSuccess ||
                    caps.UsagePage != UsagePage ||
                    caps.Usage != Usage ||
                    caps.FeatureReportByteLength < 33)
                    continue;

                var response = Query(handle, caps.FeatureReportByteLength);
                if (response is null)
                    continue;

                // Some receivers need a second status request immediately after the first.
                if (response[6] == 0)
                    response = Query(handle, caps.FeatureReportByteLength) ?? response;

                var percent = response[18];
                if (response[0] != 0x0C || response[2] != 0x20 || percent > 100)
                    continue;

                var state = response[19];
                var connected = response[6] != 0 || percent > 0 || state == 2;
                var isFullyCharged = state == 2;
                var isCharging = state is 1 or 2;
                int? reportedPercent = !connected
                    ? null
                    : isFullyCharged ? 100 : percent;

                var modelCode = Encoding.ASCII.GetString(response, 8, 4).Trim('\0', ' ');
                return new DeluxM800BatteryResult(
                    connected, reportedPercent, isCharging, isFullyCharged, modelCode);
            }
            finally
            {
                _ = HidD_FreePreparsedData(preparsedData);
            }
        }

        return null;
    }

    private static byte[]? Query(SafeFileHandle handle, ushort reportLength)
    {
        var sequence = unchecked((byte)Interlocked.Increment(ref _sequence));
        var request = new byte[reportLength];
        request[0] = 0x0C;
        request[1] = 0x01;
        request[2] = 0x20;
        request[4] = sequence;

        if (HidD_SetFeature(handle, request, request.Length) == 0)
            return null;

        Thread.Sleep(20);

        var response = new byte[reportLength];
        response[0] = 0x0C;
        return HidD_GetFeature(handle, response, response.Length) != 0 ? response : null;
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

    [DllImport("hid.dll", EntryPoint = "HidD_GetAttributes")]
    private static extern byte HidD_GetAttributes(SafeFileHandle handle, ref HidAttributes attributes);

    [DllImport("hid.dll", EntryPoint = "HidD_GetPreparsedData")]
    private static extern byte HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsedData);

    [DllImport("hid.dll", EntryPoint = "HidD_FreePreparsedData")]
    private static extern byte HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", EntryPoint = "HidP_GetCaps")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, ref HidpCaps capabilities);

    [DllImport("hid.dll", EntryPoint = "HidD_SetFeature")]
    private static extern byte HidD_SetFeature(SafeFileHandle handle, byte[] report, int reportLength);

    [DllImport("hid.dll", EntryPoint = "HidD_GetFeature")]
    private static extern byte HidD_GetFeature(SafeFileHandle handle, byte[] report, int reportLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
}
