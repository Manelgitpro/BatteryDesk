using BatteryDesk.Devices.HID;
using System.Collections.ObjectModel;
using System.Windows;

namespace BatteryDesk;

public partial class MainWindow : Window
{
    private readonly HidEnumerator _enumerator = new();
    private readonly ObservableCollection<DeviceRow> _devices = new();

    public MainWindow()
    {
        InitializeComponent();
        DevicesList.ItemsSource = _devices;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "Procurando interfaces HID...";
        StatusDetailText.Text = "Consultando descritores locais. Nenhum comando de escrita será enviado.";

        try
        {
            var found = await Task.Run(_enumerator.Enumerate);
            _devices.Clear();

            foreach (var device in found)
                _devices.Add(DeviceRow.From(device));

            DeviceCountText.Text = $"{_devices.Count} interfaces";
            StatusText.Text = _devices.Count == 0
                ? "Nenhuma interface encontrada"
                : "Descoberta concluída";
            StatusDetailText.Text = _devices.Count == 0
                ? "O Windows não retornou interfaces HID presentes. Verifique as conexões."
                : $"Foram encontradas {_devices.Count} interfaces HID. Algumas podem pertencer ao mesmo periférico ou ao próprio computador.";
            LastScanText.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
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
