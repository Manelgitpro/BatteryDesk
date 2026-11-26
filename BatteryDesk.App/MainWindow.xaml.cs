using BatteryDesk.Devices.HID;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BatteryDesk;

public partial class MainWindow : Window
{
    private readonly HidEnumerator _enumerator = new();
    private readonly DeluxM800BatteryReader _deluxReader = new();
    private readonly ZeusProPassiveCapture _zeusCapture = new();
    private readonly ObservableCollection<DeviceRow> _devices = new();
    private readonly ZeusChargeSession _zeusChargeSession = new();
    private readonly DispatcherTimer _zeusChargeDisplayTimer = new();
    private readonly TrayPreferencesStore _trayPreferencesStore = new();
    private readonly BatteryTrayIconService _trayIconService = new();
    private readonly TrayPreferences _trayPreferences;
    private IReadOnlyList<HidDeviceInfo> _zeusInterfaces = Array.Empty<HidDeviceInfo>();
    private HwndSource? _windowSource;
    private bool _devicePresenceCheckRunning;
    private bool _initializingTrayOptions = true;
    private bool _allowExit;

    private const int WmDeviceChange = 0x0219;
    private const int DbtDevNodesChanged = 0x0007;

    public MainWindow()
    {
        _trayPreferences = _trayPreferencesStore.Load();

        InitializeComponent();
        DevicesList.ItemsSource = _devices;

        ShowDeluxInTrayCheckBox.IsChecked = _trayPreferences.ShowDeluxMouse;
        ShowZeusInTrayCheckBox.IsChecked = _trayPreferences.ShowZeusHeadset;
        _initializingTrayOptions = false;

        _trayIconService.ShowRequested += Tray_ShowRequested;
        _trayIconService.ExitRequested += Tray_ExitRequested;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        UpdateTrayIcons();

        _zeusChargeDisplayTimer.Interval = TimeSpan.FromSeconds(1);
        _zeusChargeDisplayTimer.Tick += (_, _) => UpdateZeusChargeUi();
        _zeusChargeDisplayTimer.Start();

        Loaded += MainWindow_Loaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _windowSource?.AddHook(WindowProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        _zeusChargeDisplayTimer.Stop();
        _windowSource?.RemoveHook(WindowProc);
        _trayIconService.Dispose();
        base.OnClosed(e);
    }

    private bool HasSelectedTrayIcons =>
        _trayPreferences.ShowDeluxMouse || _trayPreferences.ShowZeusHeadset;

    private void TraySelection_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingTrayOptions)
            return;

        _trayPreferences.ShowDeluxMouse = ShowDeluxInTrayCheckBox.IsChecked == true;
        _trayPreferences.ShowZeusHeadset = ShowZeusInTrayCheckBox.IsChecked == true;
        _trayPreferencesStore.Save(_trayPreferences);
        UpdateTrayIcons();
    }

    private void UpdateTrayIcons()
    {
        var now = DateTimeOffset.Now;
        var zeusPercent = _zeusChargeSession.GetEstimatedBatteryPercent(now);
        var zeusDescription = _zeusChargeSession.WasInterruptedWhileAppClosed
            ? "sessão de carga interrompida"
            : _zeusChargeSession.IsUsbConnected
                ? "estimativa durante a carga"
                : _zeusChargeSession.EndedAt.HasValue
                    ? "estimativa após desconectar; supõe uso contínuo"
                    : "sem sessão de carga registrada";

        _trayIconService.Update(
            _trayPreferences,
            _trayPreferences.LastDeluxBatteryPercent,
            _trayPreferences.LastDeluxReadingAt,
            zeusPercent,
            zeusDescription);
    }

    private void Tray_ShowRequested()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }));
    }

    private void Tray_ExitRequested()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _allowExit = true;
            Close();
        }));
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowExit)
            return;

        if (HasSelectedTrayIcons)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _allowExit = true;
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && HasSelectedTrayIcons)
            Hide();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await CheckZeusUsbConnectionAsync(isStartup: true);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmDeviceChange && wParam.ToInt32() == DbtDevNodesChanged)
            _ = CheckZeusUsbConnectionAsync();

        return IntPtr.Zero;
    }

    private async Task CheckZeusUsbConnectionAsync(bool isStartup = false)
    {
        if (_devicePresenceCheckRunning)
            return;

        _devicePresenceCheckRunning = true;
        try
        {
            if (!isStartup)
                await Task.Delay(600);

            var devices = await Task.Run(_enumerator.Enumerate);
            var cableConnected = IsZeusUsbChargingCablePresent(devices);

            if (isStartup)
                _zeusChargeSession.Initialize(cableConnected, DateTimeOffset.Now);
            else
                _zeusChargeSession.SetUsbConnected(cableConnected, DateTimeOffset.Now);

            UpdateZeusChargeUi();
            UpdateTrayIcons();
        }
        catch (Exception exception)
        {
            ZeusChargeDetailText.Text =
                $"Não foi possível verificar a conexão USB-C: {exception.Message}";
        }
        finally
        {
            _devicePresenceCheckRunning = false;
        }
    }

    private static bool IsZeusUsbChargingCablePresent(IEnumerable<HidDeviceInfo> devices) =>
        devices.Any(device =>
            device.VendorId == 0x040B &&
            device.ProductId == 0x0897 &&
            device.Product.Contains("H510-PRO", StringComparison.OrdinalIgnoreCase) &&
            device.Product.Contains("USB", StringComparison.OrdinalIgnoreCase) &&
            !device.Product.Contains("Wireless", StringComparison.OrdinalIgnoreCase));

    private void UpdateZeusChargeUi()
    {
        var startedAt = _zeusChargeSession.StartedAt;
        ResetZeusChargeButton.IsEnabled = startedAt.HasValue ||
                                          _zeusChargeSession.WasInterruptedWhileAppClosed;

        if (!startedAt.HasValue)
        {
            ZeusChargeStatusText.Text = _zeusChargeSession.IsUsbConnected
                ? "USB-C conectado; preparando cronômetro..."
                : "Aguardando conexão USB-C...";
            ZeusChargeTimerText.Text = "--:--:--";
            ZeusChargeProgressText.Text = "Tempo decorrido: aguardando início";
            ZeusChargeProgressBar.Value = 0;
            ZeusChargeDetailText.Text =
                "Conecte o H510-PRO ao computador pelo cabo USB-C. O dongle 2,4 GHz não inicia este cronômetro.";
            UpdateTrayIcons();
            return;
        }

        if (_zeusChargeSession.WasInterruptedWhileAppClosed)
        {
            var lastStart = startedAt.Value.ToLocalTime().ToString("dd/MM HH:mm");
            ZeusChargeStatusText.Text = "Sessão interrompida com o BatteryDesk fechado";
            ZeusChargeTimerText.Text = "--:--:--";
            ZeusChargeProgressText.Text = $"Último início registrado: {lastStart}";
            ZeusChargeProgressBar.Value = 0;
            ZeusChargeDetailText.Text =
                "O cabo foi removido ou a conexão mudou enquanto o aplicativo estava fechado. Não é possível saber quando isso ocorreu. Use “Resetar ciclo” antes da próxima carga.";
            UpdateTrayIcons();
            return;
        }

        var referenceEnd = _zeusChargeSession.EndedAt ?? DateTimeOffset.Now;
        var elapsed = referenceEnd - startedAt.Value;
        if (elapsed < TimeSpan.Zero)
            elapsed = TimeSpan.Zero;

        var estimate = ZeusChargeSession.EstimatedFullChargeTime;
        var progress = Math.Clamp(elapsed.TotalSeconds / estimate.TotalSeconds * 100d, 0d, 100d);
        ZeusChargeProgressBar.Value = progress;
        ZeusChargeProgressText.Text =
            $"Tempo contado: {FormatDuration(elapsed)} de {FormatDuration(estimate)}";

        if (_zeusChargeSession.IsUsbConnected && !_zeusChargeSession.EndedAt.HasValue)
        {
            var remaining = estimate - elapsed;
            if (remaining > TimeSpan.Zero)
            {
                ZeusChargeStatusText.Text = "Carregamento USB-C detectado";
                ZeusChargeTimerText.Text = FormatDuration(remaining);
                var completionTime = startedAt.Value.Add(estimate).ToLocalTime().ToString("HH:mm");
                ZeusChargeDetailText.Text =
                    $"Tempo de referência da Redragon: aproximadamente 3 horas. Previsão de conclusão às {completionTime}. " +
                    "É uma estimativa por tempo, não uma leitura da bateria.";
                if (_zeusChargeSession.StartedAtAppLaunch)
                {
                    ZeusChargeDetailText.Text +=
                        " O cabo já estava conectado quando o BatteryDesk abriu; o tempo anterior não foi contado.";
                }
            }
            else
            {
                ZeusChargeStatusText.Text = "Tempo estimado de carga alcançado";
                ZeusChargeTimerText.Text = "00:00:00";
                ZeusChargeDetailText.Text =
                    "A referência de aproximadamente 3 horas foi alcançada. Isso não confirma 100% de carga. Confira o indicador do headset e desconecte o USB quando confirmar.";
            }
        }
        else if (_zeusChargeSession.EndedAt.HasValue)
        {
            ZeusChargeTimerText.Text = FormatDuration(elapsed);
            if (elapsed >= estimate)
            {
                ZeusChargeStatusText.Text = "Cabo USB-C removido após o tempo estimado";
                ZeusChargeDetailText.Text =
                    "A sessão ultrapassou a referência de aproximadamente 3 horas. O BatteryDesk não consegue confirmar a carga real ou 100%.";
            }
            else
            {
                ZeusChargeStatusText.Text = "Carregamento USB-C encerrado";
                ZeusChargeDetailText.Text =
                    $"O cabo foi removido após {FormatDuration(elapsed)}. A referência de carga de aproximadamente 3 horas não foi completada; isso não informa a porcentagem real.";
            }
        }

        UpdateTrayIcons();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var hours = Math.Max(0, (int)duration.TotalHours);
        return $"{hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void ResetZeusCharge_Click(object sender, RoutedEventArgs e)
    {
        var message = _zeusChargeSession.IsUsbConnected
            ? "Reiniciar a contagem estimativa a partir de agora? O cabo continuará conectado. Nenhum comando será enviado ao headset."
            : "Apagar o ciclo de carga registrado e aguardar a próxima conexão USB-C? Nenhum comando será enviado ao headset.";

        var answer = MessageBox.Show(
            this,
            message,
            "Resetar cronômetro do Zeus Pro",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
            return;

        _zeusChargeSession.Reset(DateTimeOffset.Now);
        UpdateZeusChargeUi();
        UpdateTrayIcons();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        CaptureZeusButton.IsEnabled = false;
        StatusText.Text = "Procurando dispositivos...";
        StatusDetailText.Text = "Lendo descritores HID, identificando coleções, interfaces e tamanhos dos relatórios.";

        try
        {
            var found = await Task.Run(_enumerator.Enumerate);
            _zeusChargeSession.SetUsbConnected(
                IsZeusUsbChargingCablePresent(found), DateTimeOffset.Now);
            UpdateZeusChargeUi();

            _zeusInterfaces = found
                .Where(device => device.VendorId == 0x040B && device.ProductId == 0x0897)
                .ToArray();

            var zeusVendorInterfaceFound = _zeusInterfaces.Any(device =>
                device.UsagePage == 0xF100 && device.InputReportByteLength > 0);
            CaptureZeusButton.IsEnabled = zeusVendorInterfaceFound;
            ZeusCaptureStatusText.Text = zeusVendorInterfaceFound
                ? "Canal proprietário 0xF100 encontrado. A captura passiva está disponível."
                : "A coleção proprietária 0xF100 não foi encontrada nesta varredura.";

            var deluxInterfaces = found
                .Where(device => device.VendorId == 0x248A && device.ProductId == 0x5B2F)
                .ToArray();

            var keyboardInterfaces = found
                .Where(device => device.VendorId == 0x1A2C && device.ProductId == 0x9605)
                .GroupBy(device => device.PhysicalGroupKey)
                .Select(group => group.ToArray())
                .ToArray();

            DeluxM800BatteryResult? deluxReading = null;
            var deluxQueryAuthorized = false;

            if (deluxInterfaces.Length > 0)
            {
                var consent = MessageBox.Show(
                    this,
                    "O BatteryDesk encontrou o receptor Delux M800 Pro. Para consultar a bateria, precisa enviar uma solicitação HID de telemetria (Feature Report 0C; pode repetir a solicitação se o receptor pedir estado) e ler a resposta.\n\n" +
                    "A solicitação é específica para obter o estado da bateria; não grava configurações, não altera firmware e não executa comandos de configuração.\n\n" +
                    "Autoriza esta consulta agora? Se escolher Não, nenhum pacote será enviado ao receptor.",
                    "Autorizar consulta de bateria do Delux",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (consent == MessageBoxResult.Yes)
                {
                    deluxQueryAuthorized = true;
                    deluxReading = await Task.Run(() =>
                        _deluxReader.Read(deluxInterfaces, explicitlyAuthorizedByUser: true));
                }
            }

            if (deluxReading?.Percent is int latestDeluxPercent)
            {
                _trayPreferences.LastDeluxBatteryPercent = latestDeluxPercent;
                _trayPreferences.LastDeluxReadingAt = DateTimeOffset.Now;
                _trayPreferencesStore.Save(_trayPreferences);
            }

            UpdateTrayIcons();
            _devices.Clear();

            var deluxGroupAdded = false;
            var keyboardGroupsAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var device in found)
            {
                var isDelux = device.VendorId == 0x248A && device.ProductId == 0x5B2F;
                if (isDelux)
                {
                    if (deluxGroupAdded)
                        continue;

                    deluxGroupAdded = true;
                    _devices.Add(DeviceRow.From(
                        device, deluxInterfaces.Length, deluxReading, isDeluxReceiver: true, deluxQueryAuthorized: deluxQueryAuthorized));
                    continue;
                }

                var isSemicoKeyboard = device.VendorId == 0x1A2C && device.ProductId == 0x9605;
                if (isSemicoKeyboard)
                {
                    var groupKey = device.PhysicalGroupKey;
                    if (!keyboardGroupsAdded.Add(groupKey))
                        continue;

                    var group = keyboardInterfaces.FirstOrDefault(g =>
                        g.Any(item => string.Equals(item.PhysicalGroupKey, groupKey, StringComparison.OrdinalIgnoreCase)));

                    if (group is { Length: > 0 })
                        _devices.Add(DeviceRow.FromKeyboardGroup(group));
                    continue;
                }

                _devices.Add(DeviceRow.From(device));
            }

            DeviceCountText.Text = $"{found.Count} interfaces · {_devices.Count} dispositivos";
            LastScanText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            var keyboardSummary = keyboardInterfaces.Length > 0
                ? $" Teclado SEMICO: {keyboardInterfaces.Sum(group => group.Length)} coleções/interfaces analisadas."
                : " Teclado SEMICO não conectado nesta varredura.";

            if (deluxInterfaces.Length > 0 && deluxReading?.Percent is int percent)
            {
                var chargeState = deluxReading.IsFullyCharged
                    ? "carga completa"
                    : deluxReading.IsCharging ? "carregando" : "descarregando";
                StatusText.Text = $"Delux M800 Pro: {percent}%";
                StatusDetailText.Text = $"Leitura recebida pelo receptor 2,4 GHz ({chargeState}).{keyboardSummary}";
            }
            else if (deluxInterfaces.Length > 0 && !deluxQueryAuthorized)
            {
                StatusText.Text = "Consulta do Delux não autorizada";
                StatusDetailText.Text = "Nenhum pacote foi enviado ao receptor. A identificação HID foi somente de leitura." + keyboardSummary;
            }
            else if (deluxInterfaces.Length > 0)
            {
                StatusText.Text = "Receptor Delux identificado";
                StatusDetailText.Text = "A consulta foi autorizada, mas o receptor ainda não respondeu com dados válidos." + keyboardSummary;
            }
            else
            {
                StatusText.Text = found.Count == 0
                    ? "Nenhuma interface encontrada"
                    : "Descoberta concluída";
                StatusDetailText.Text = found.Count == 0
                    ? "O Windows não retornou interfaces HID presentes. Verifique as conexões."
                    : $"Foram encontradas {found.Count} interfaces HID. Nenhum comando de leitura de bateria do Delux foi executado." + keyboardSummary;
            }
        }
        catch (Exception exception)
        {
            StatusText.Text = "Falha na descoberta";
            StatusDetailText.Text = exception.Message;
            LastScanText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private async void CaptureZeus_Click(object sender, RoutedEventArgs e)
    {
        var captureInterfaces = _zeusInterfaces
            .Where(device =>
                device.InputReportByteLength > 0 &&
                device.UsagePage == 0xF100)
            .ToArray();

        if (!captureInterfaces.Any(device => device.UsagePage == 0xF100))
        {
            ZeusCaptureStatusText.Text = "A coleção proprietária 0xF100 não está disponível. Faça uma nova varredura.";
            return;
        }

        CaptureZeusButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Capturando relatórios passivamente...";
        StatusDetailText.Text = "Durante 30 segundos, deixe o headset em repouso. A captura apenas lê relatórios que ele transmitir espontaneamente; nenhum comando será enviado.";
        ZeusCaptureStatusText.Text = "Captura passiva em andamento. Não é necessário tocar no headset.";

        try
        {
            var result = await _zeusCapture.CaptureAsync(
                captureInterfaces,
                TimeSpan.FromSeconds(30));

            StatusText.Text = result.Frames.Count > 0
                ? $"{result.Frames.Count} relatórios HID recebidos"
                : "Nenhum relatório HID recebido";
            StatusDetailText.Text = result.Frames.Count > 0
                ? "A captura foi salva. Os dados ainda precisam ser analisados para verificar se existe algum campo relacionado à bateria."
                : "As interfaces monitoradas não enviaram relatórios durante o teste. Isso não descarta mensagens em outros momentos ou tráfego USB fora do HID.";

            ZeusCaptureStatusText.Text =
                $"Arquivo: {result.LogPath}{Environment.NewLine}" +
                $"Interfaces: {result.InterfaceCount} · Relatórios: {result.Frames.Count} · Erros: {result.Errors.Count}";
        }
        catch (Exception exception)
        {
            StatusText.Text = "Falha na captura passiva";
            StatusDetailText.Text = exception.Message;
            ZeusCaptureStatusText.Text = "A captura não foi concluída. Faça uma nova varredura e tente novamente.";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            CaptureZeusButton.IsEnabled = _zeusInterfaces.Any(device =>
                device.UsagePage == 0xF100 && device.InputReportByteLength > 0);
        }
    }
}
