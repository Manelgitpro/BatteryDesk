using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;

namespace BatteryDesk;

/// <summary>
/// Manages one Windows notification-area icon per selected device.
/// Values are supplied by the app; this service never communicates with peripherals.
/// </summary>
internal sealed class BatteryTrayIconService : IDisposable
{
    private readonly Forms.ContextMenuStrip _contextMenu = new();
    private Forms.NotifyIcon? _deluxNotifyIcon;
    private Forms.NotifyIcon? _zeusNotifyIcon;
    private DrawingIcon? _deluxIcon;
    private DrawingIcon? _zeusIcon;
    private string? _deluxStateKey;
    private string? _zeusStateKey;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public BatteryTrayIconService()
    {
        var openItem = new Forms.ToolStripMenuItem("Abrir BatteryDesk");
        openItem.Click += (_, _) => ShowRequested?.Invoke();

        var exitItem = new Forms.ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(new Forms.ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);
    }

    public void Update(
        TrayPreferences preferences,
        int? deluxPercent,
        DateTimeOffset? deluxUpdatedAt,
        int? zeusEstimatedPercent,
        string zeusEstimateDescription)
    {
        if (_disposed)
            return;

        UpdateDeluxIcon(preferences.ShowDeluxMouse, deluxPercent, deluxUpdatedAt);
        UpdateZeusIcon(preferences.ShowZeusHeadset, zeusEstimatedPercent, zeusEstimateDescription);
    }

    private void UpdateDeluxIcon(bool visible, int? percent, DateTimeOffset? updatedAt)
    {
        if (!visible)
        {
            if (_deluxNotifyIcon is not null)
                _deluxNotifyIcon.Visible = false;
            return;
        }

        EnsureNotifyIcon(ref _deluxNotifyIcon);
        var isOldReading = updatedAt.HasValue &&
                           DateTimeOffset.Now - updatedAt.Value > TimeSpan.FromHours(6);
        var updated = updatedAt.HasValue
            ? $" · {updatedAt.Value.ToLocalTime():dd/MM HH:mm}" + (isOldReading ? " · antiga" : string.Empty)
            : string.Empty;
        var text = percent.HasValue
            ? $"Delux M800 Pro: {percent.Value}%{updated}"
            : "Delux M800 Pro: sem leitura. Use Procurar dispositivos e autorize a consulta.";
        var key = $"delux|{percent?.ToString() ?? "unknown"}|{updatedAt?.ToUnixTimeSeconds().ToString() ?? "none"}";

        if (_deluxStateKey != key)
        {
            ReplaceIcon(ref _deluxNotifyIcon, ref _deluxIcon, false, percent, false, text);
            _deluxStateKey = key;
        }
        _deluxNotifyIcon!.Text = LimitTooltip(text);
        _deluxNotifyIcon.Visible = true;
    }

    private void UpdateZeusIcon(bool visible, int? percent, string description)
    {
        if (!visible)
        {
            if (_zeusNotifyIcon is not null)
                _zeusNotifyIcon.Visible = false;
            return;
        }

        EnsureNotifyIcon(ref _zeusNotifyIcon);
        var text = percent.HasValue
            ? $"Zeus Pro: ~{percent.Value}% estimados · {description}"
            : "Zeus Pro: sem estimativa. Registre uma sessão de carga pelo USB-C.";
        var key = $"zeus|{percent?.ToString() ?? "unknown"}";

        if (_zeusStateKey != key)
        {
            ReplaceIcon(ref _zeusNotifyIcon, ref _zeusIcon, true, percent, true, text);
            _zeusStateKey = key;
        }
        _zeusNotifyIcon!.Text = LimitTooltip(text);
        _zeusNotifyIcon.Visible = true;
    }

    private void EnsureNotifyIcon(ref Forms.NotifyIcon? notifyIcon)
    {
        if (notifyIcon is not null)
            return;

        notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = _contextMenu,
            Visible = false
        };
        notifyIcon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    private static void ReplaceIcon(
        ref Forms.NotifyIcon? notifyIcon,
        ref DrawingIcon? currentIcon,
        bool headset,
        int? percent,
        bool estimated,
        string tooltip)
    {
        var replacement = CreateDeviceIcon(headset, percent, estimated);
        var previous = currentIcon;
        currentIcon = replacement;
        notifyIcon!.Icon = replacement;
        notifyIcon.Text = LimitTooltip(tooltip);
        previous?.Dispose();
    }

    private static DrawingIcon CreateDeviceIcon(bool headset, int? percent, bool estimated)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.Clear(Color.Transparent);

            using var devicePen = new Pen(Color.FromArgb(238, 245, 248), 2.4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var accentBrush = new SolidBrush(headset
                ? Color.FromArgb(76, 190, 170)
                : Color.FromArgb(112, 169, 235));

            if (headset)
            {
                graphics.DrawArc(devicePen, 5, 2, 22, 25, 200, 140);
                graphics.DrawArc(devicePen, 4, 13, 7, 12, 90, 180);
                graphics.DrawArc(devicePen, 21, 13, 7, 12, 270, 180);
                graphics.FillRectangle(accentBrush, 4, 14, 5, 9);
                graphics.FillRectangle(accentBrush, 23, 14, 5, 9);
            }
            else
            {
                using var mousePath = RoundedRectangle(new Rectangle(8, 2, 16, 25), 7);
                graphics.DrawPath(devicePen, mousePath);
                graphics.DrawLine(devicePen, 16, 4, 16, 12);
                graphics.DrawLine(devicePen, 12, 12, 20, 12);
                graphics.FillEllipse(accentBrush, 14, 6, 4, 4);
            }

            using var badgeBrush = new SolidBrush(Color.FromArgb(18, 27, 39));
            using var badgePath = RoundedRectangle(new Rectangle(5, 21, 27, 11), 4);
            graphics.FillPath(badgeBrush, badgePath);

            var label = percent.HasValue
                ? $"{(estimated ? "~" : string.Empty)}{percent.Value}"
                : "--";
            using var labelFont = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var labelBrush = new SolidBrush(Color.White);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            graphics.DrawString(label, labelFont, labelBrush, new RectangleF(5, 20, 27, 12), format);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            var borrowed = DrawingIcon.FromHandle(hIcon);
            return (DrawingIcon)borrowed.Clone();
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string LimitTooltip(string text) =>
        text.Length <= 63 ? text : text[..60] + "...";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeNotifyIcon(_deluxNotifyIcon);
        DisposeNotifyIcon(_zeusNotifyIcon);
        _deluxIcon?.Dispose();
        _zeusIcon?.Dispose();
        _contextMenu.Dispose();
    }

    private static void DisposeNotifyIcon(Forms.NotifyIcon? notifyIcon)
    {
        if (notifyIcon is null)
            return;

        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
