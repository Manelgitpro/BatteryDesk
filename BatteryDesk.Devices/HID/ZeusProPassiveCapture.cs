using Microsoft.Win32.SafeHandles;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BatteryDesk.Devices.HID;

public sealed record HidPassiveCaptureFrame(
    DateTimeOffset TimestampUtc,
    ushort UsagePage,
    ushort Usage,
    string ReportHex);

public sealed record HidPassiveCaptureResult(
    string LogPath,
    TimeSpan Duration,
    int InterfaceCount,
    IReadOnlyList<HidPassiveCaptureFrame> Frames,
    IReadOnlyList<string> Errors);

/// <summary>
/// Escuta somente relatórios HID de entrada do H510-PRO. Não envia Output Reports,
/// Feature Reports, comandos de consulta ou qualquer dado ao headset.
/// </summary>
public sealed class ZeusProPassiveCapture
{
    private const ushort VendorId = 0x040B;
    private const ushort ProductId = 0x0897;
    private const ushort VendorUsagePage = 0xF100;
    private const uint GenericRead = 0x80000000;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;
    private const int MaxFramesPerInterface = 512;

    private static readonly HashSet<ushort> CapturedUsagePages = [0xF100, 0x000C, 0x000B];

    public async Task<HidPassiveCaptureResult> CaptureAsync(
        IReadOnlyCollection<HidDeviceInfo> interfaces,
        TimeSpan duration,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("A captura HID está disponível somente no Windows.");

        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(duration), "A captura deve durar entre 1 segundo e 2 minutos.");

        var targets = interfaces
            .Where(device =>
                device.VendorId == VendorId &&
                device.ProductId == ProductId &&
                CapturedUsagePages.Contains(device.UsagePage) &&
                device.InputReportByteLength > 0)
            .GroupBy(device => device.DevicePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (!targets.Any(device => device.UsagePage == VendorUsagePage))
            throw new InvalidOperationException(
                "A coleção proprietária HID 0xF100 do H510-PRO não foi encontrada nesta varredura.");

        if (targets.Length == 0)
            throw new InvalidOperationException("Nenhuma interface HID de entrada compatível foi encontrada.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        var started = Stopwatch.StartNew();

        var tasks = targets.Select(device => ListenAsync(device, timeout.Token)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        started.Stop();

        var frames = results
            .SelectMany(result => result.Frames)
            .OrderBy(frame => frame.TimestampUtc)
            .ToArray();
        var errors = results
            .Where(result => result.Error is not null)
            .Select(result => result.Error!)
            .ToArray();

        progress?.Report(frames.Length);

        var logPath = SaveLog(targets, started.Elapsed, frames, errors);
        return new HidPassiveCaptureResult(logPath, started.Elapsed, targets.Length, frames, errors);
    }

    private static async Task<InterfaceCaptureResult> ListenAsync(
        HidDeviceInfo device,
        CancellationToken cancellationToken)
    {
        var handle = CreateFile(
            device.DevicePath,
            GenericRead,
            ShareRead | ShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOverlapped,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var errorCode = Marshal.GetLastWin32Error();
            handle.Dispose();
            return new InterfaceCaptureResult(
                [],
                $"Uso {device.UsagePage:X4}: não foi possível abrir a interface (erro Win32 {errorCode}).");
        }

        try
        {
            await using var stream = new FileStream(
                handle,
                FileAccess.Read,
                Math.Max(device.InputReportByteLength, 64),
                isAsync: true);

            var buffer = new byte[device.InputReportByteLength];
            var frames = new List<HidPassiveCaptureFrame>(32);

            while (!cancellationToken.IsCancellationRequested && frames.Count < MaxFramesPerInterface)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (IOException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (bytesRead <= 0)
                    break;

                frames.Add(new HidPassiveCaptureFrame(
                    DateTimeOffset.UtcNow,
                    device.UsagePage,
                    device.Usage,
                    Convert.ToHexString(buffer.AsSpan(0, bytesRead))));
            }

            return new InterfaceCaptureResult(frames, null);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or Win32Exception or ArgumentException)
        {
            handle.Dispose();
            return new InterfaceCaptureResult(
                [],
                $"Uso {device.UsagePage:X4}: falha de leitura ({exception.GetType().Name}).");
        }
    }

    private static string SaveLog(
        IReadOnlyCollection<HidDeviceInfo> interfaces,
        TimeSpan duration,
        IReadOnlyList<HidPassiveCaptureFrame> frames,
        IReadOnlyList<string> errors)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryDesk",
            "Captures");
        Directory.CreateDirectory(directory);

        var fileName = $"H510-PRO-USB-passive-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var path = Path.Combine(directory, fileName);
        var builder = new StringBuilder();

        builder.AppendLine("BatteryDesk - captura HID passiva do Redragon Zeus Pro H510-PRO");
        builder.AppendLine("Modo: somente leitura. Nenhum Output Report ou Feature Report foi enviado.");
        builder.AppendLine($"VID:PID: {VendorId:X4}:{ProductId:X4}");
        builder.AppendLine($"Início UTC: {DateTimeOffset.UtcNow.Subtract(duration):O}");
        builder.AppendLine($"Duração aproximada: {duration.TotalSeconds:F1} s");
        builder.AppendLine("Coleções monitoradas:");
        foreach (var device in interfaces.OrderBy(device => device.UsagePage))
            builder.AppendLine($"  Usage Page 0x{device.UsagePage:X4}, Usage 0x{device.Usage:X4}, Input {device.InputReportByteLength} bytes");
        builder.AppendLine();
        builder.AppendLine($"Relatórios recebidos: {frames.Count}");

        foreach (var frame in frames)
        {
            builder.Append(frame.TimestampUtc.ToString("O"));
            builder.Append($" | PAGE=0x{frame.UsagePage:X4} USAGE=0x{frame.Usage:X4} | ");
            builder.AppendLine(frame.ReportHex);
        }

        if (errors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Erros de leitura:");
            foreach (var error in errors)
                builder.AppendLine(error);
        }

        builder.AppendLine();
        builder.AppendLine("Observação: relatórios HID observados não são, por si só, prova de que contenham bateria.");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private sealed record InterfaceCaptureResult(
        IReadOnlyList<HidPassiveCaptureFrame> Frames,
        string? Error);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
