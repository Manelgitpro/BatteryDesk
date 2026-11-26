using BatteryDesk.Devices.HID;
using System.Collections.ObjectModel;
using System.Windows;

namespace BatteryDesk;

public partial class MainWindow : Window
{
    private readonly HidEnumerator _enumerator = new();
    private readonly DeluxM800BatteryReader _deluxReader = new();
    private readonly ObservableCollection<DeviceRow> _devices = new();

    public MainWindow()
    {
        InitializeComponent();
        DevicesList.ItemsSource = _devices;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Procurando dispositivos...";
        StatusDetailText.Text = "Lendo descritores HID, identificando coleções, interfaces e tamanhos dos relatórios.";

        try
        {
            var found = await Task.Run(_enumerator.Enumerate);
            var deluxInterfaces = found
                .Where(device => device.VendorId == 0x248A && device.ProductId == 0x5B2F)
                .ToArray();

            var keyboardInterfaces = found
                .Where(device => device.VendorId == 0x1A2C && device.ProductId == 0x9605)
                .GroupBy(device => device.PhysicalGroupKey)
                .Select(group => group.ToArray())
                .ToArray();

            DeluxM800BatteryResult? deluxReading = null;
            if (deluxInterfaces.Length > 0)
                deluxReading = await Task.Run(() => _deluxReader.Read(deluxInterfaces));

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
                        device, deluxInterfaces.Length, deluxReading, isDeluxReceiver: true));
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
            else if (deluxInterfaces.Length > 0)
            {
                StatusText.Text = "Receptor Delux identificado";
                StatusDetailText.Text = "O receptor 2,4 GHz foi encontrado, mas ainda não respondeu à consulta." + keyboardSummary;
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
}
