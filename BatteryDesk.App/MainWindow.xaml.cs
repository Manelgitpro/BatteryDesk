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
        StatusDetailText.Text = "Enumerando interfaces HID e consultando apenas o protocolo conhecido do receptor Delux M800 Pro.";

        try
        {
            var found = await Task.Run(_enumerator.Enumerate);
            var deluxInterfaces = found
                .Where(device => device.VendorId == 0x248A && device.ProductId == 0x5B2F)
                .ToArray();

            DeluxM800BatteryResult? deluxReading = null;
            if (deluxInterfaces.Length > 0)
                deluxReading = await Task.Run(() => _deluxReader.Read(deluxInterfaces));

            _devices.Clear();

            // Collapse the receiver's multiple HID top-level collections into one physical-device card.
            var deluxGroupAdded = false;
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

                _devices.Add(DeviceRow.From(device));
            }

            DeviceCountText.Text = $"{found.Count} interfaces · {_devices.Count} dispositivos";
            LastScanText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

            if (deluxInterfaces.Length > 0 && deluxReading?.Percent is int percent)
            {
                var chargeState = deluxReading.IsFullyCharged
                    ? "carga completa"
                    : deluxReading.IsCharging ? "carregando" : "descarregando";
                StatusText.Text = $"Delux M800 Pro: {percent}%";
                StatusDetailText.Text = $"Leitura recebida diretamente pelo receptor USB de 2,4 GHz ({chargeState}).";
            }
            else if (deluxInterfaces.Length > 0)
            {
                StatusText.Text = "Receptor Delux identificado";
                StatusDetailText.Text = "O receptor 2,4 GHz foi encontrado, mas ainda não respondeu à consulta. Deixe o mouse ligado e conectado ao receptor e tente novamente.";
            }
            else
            {
                StatusText.Text = found.Count == 0
                    ? "Nenhuma interface encontrada"
                    : "Descoberta concluída";
                StatusDetailText.Text = found.Count == 0
                    ? "O Windows não retornou interfaces HID presentes. Verifique as conexões."
                    : $"Foram encontradas {found.Count} interfaces HID. A consulta de bateria pelo receptor Delux não foi executada porque ele não está conectado.";
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
