using BatteryDesk.Devices.HID;
using System.Collections.ObjectModel;
using System.Windows;

namespace BatteryDesk;

public partial class MainWindow : Window
{
    private readonly HidEnumerator _enumerator = new();
    private readonly DeluxM800BatteryReader _deluxReader = new();
    private readonly ZeusProPassiveCapture _zeusCapture = new();
    private readonly ObservableCollection<DeviceRow> _devices = new();
    private IReadOnlyList<HidDeviceInfo> _zeusInterfaces = Array.Empty<HidDeviceInfo>();

    public MainWindow()
    {
        InitializeComponent();
        DevicesList.ItemsSource = _devices;
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
