using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
using LinkedRGB.Permissions;

namespace LinkedRGB;

public partial class MainPage : ContentPage
{
    private readonly IAdapter _adapter;
    private readonly ObservableCollection<DeviceRow> _devices = new();

    public MainPage()
    {
        InitializeComponent();

        _adapter = CrossBluetoothLE.Current.Adapter;
        DevicesView.ItemsSource = _devices;

        _adapter.DeviceDiscovered += (_, e) =>
        {
            var name = e.Device.Name;

            // On ignore les devices sans nom
            if (string.IsNullOrWhiteSpace(name))
                return;

            // Filtre ESP32 provisioning
            if (!name.StartsWith("PROV_"))
                return;

            if (_devices.Any(d => d.Id == e.Device.Id.ToString()))
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _devices.Add(new DeviceRow
                {
                    Name = name,
                    Id = e.Device.Id.ToString(),
                    Rssi = e.Device.Rssi,
                    Device = e.Device
                });
            });
        };
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Demande permissions...";
            var status = await MauiPermissions.RequestAsync<BluetoothScanPermission>();
            if (status != PermissionStatus.Granted)
            {
                StatusLabel.Text = "Permissions refusées.";
                return;
            }

            if (!CrossBluetoothLE.Current.IsAvailable)
            {
                StatusLabel.Text = "Bluetooth non disponible.";
                return;
            }
            if (!CrossBluetoothLE.Current.IsOn)
            {
                StatusLabel.Text = "Bluetooth désactivé (activez-le).";
                return;
            }

            _devices.Clear();
            StatusLabel.Text = "Scan en cours...";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _adapter.StartScanningForDevicesAsync(cancellationToken: cts.Token);

            StatusLabel.Text = $"Scan terminé: {_devices.Count} device(s).";
        }
        catch (OperationCanceledException)
        {
            StatusLabel.Text = $"Scan terminé: {_devices.Count} device(s).";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Erreur: {ex.Message}";
        }
    }
    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not DeviceRow row)
            return;

        try
        {
            StatusLabel.Text = $"Connexion à {row.Name}...";

            // 1️⃣ Connexion BLE
            await _adapter.ConnectToDeviceAsync(row.Device);

            StatusLabel.Text = $"Connecté à {row.Name}, découverte services...";

            // 2️⃣ VALIDATION GATT (POINT 3)
            var services = await row.Device.GetServicesAsync();

            StatusLabel.Text = $"Connecté ({services.Count} service(s))";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Erreur connexion: {ex.Message}";
        }
    }


    public class DeviceRow
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public int Rssi { get; set; }
        public IDevice Device { get; set; } = default!;
    }
}
