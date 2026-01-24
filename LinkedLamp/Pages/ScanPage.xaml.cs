#if ANDROID
using LinkedLamp.Models;
using LinkedLamp.Permissions;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif

namespace LinkedLamp.Pages;

public partial class ScanPage : ContentPage
{
#if ANDROID
    public class DeviceRow : IEquatable<DeviceRow>
    {
        public string Name { get; set; } = "";
        public IDevice Device { get; set; } = default!;

        public bool Equals(DeviceRow? other)
        {
            return other != null && Device.Name == other.Device.Name;
        }
    }

    private readonly LinkedLamp.Services.EspBleProvisioningService _prov;
    private readonly SendConfigPage _sendConfigPage;
    private readonly ObservableCollection<DeviceRow> _devices = new();
    private ProvisioningContext? _ctx;
    private CancellationTokenSource? _scanCts;
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    public ScanPage(LinkedLamp.Services.EspBleProvisioningService prov, SendConfigPage sendConfigPage)
    {
        InitializeComponent();
        _prov = prov;
        _sendConfigPage = sendConfigPage;
        DevicesView.ItemsSource = _devices;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = StartScanAsync();
    }
    protected override void OnDisappearing()
    {
        CancelScan();
        base.OnDisappearing();
    }
    public void SetContext(ProvisioningContext ctx)
    {
        _ctx = ctx;
        _devices.Clear();
        DevicesView.SelectedItem = null;
        MainLabel.Text = "Scanning for nearby LinkedLamp devices...";
        ScanButton.Text = "Scan";
        ScanButton.IsEnabled = true;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        _ = StartScanAsync();
    }

    private async Task StartScanAsync()
    {
        if (_ctx == null)
            return;

        await _scanGate.WaitAsync();
        try
        {
            CancelScan();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            ScanButton.IsEnabled = false;
            MainLabel.Text = "Scanning for nearby LinkedLamp devices...";
            _devices.Clear();

            var st = await MauiPermissions.RequestAsync<BluetoothScanPermission>();
            if (st != PermissionStatus.Granted)
            {
                MainLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth and retry.";
                return;
            }

            var btEnabled = CrossBluetoothLE.Current.State == BluetoothState.On;
            if (!btEnabled)
            {
                btEnabled = await LinkedLamp.Platforms.Android.BluetoothEnabler.RequestEnableAsync();
            }

            if (!btEnabled)
            {
                MainLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth and retry.";
                return;
            }

            HashSet<IDevice> devices;

            try
            {
                devices = await _prov.Scan(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                MainLabel.Text = $"Scan failed: {ex.Message}";
                return;
            }

            foreach (var device in devices)
            {
                _devices.Add(new DeviceRow { Name = device.Name, Device = device });
            }

            if (_devices.Count == 0)
            {
                MainLabel.Text = "No LinkedLamp device detected.\nMake sure that the BlueTooth Mode is enabled on your device (blue flashing light) and that you are standing close to it.";
            }
            else
            {
                MainLabel.Text = "Select the LinkedLamp you want to configure";
            }
        }
        finally
        {
            ScanButton.IsEnabled = true;
            _scanGate.Release();
        }
    }


    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_ctx == null)
            return;

        if (DevicesView.SelectedItem is not DeviceRow selected)
            return;

        CancelScan();

        _sendConfigPage.SetContext(_ctx, selected.Device);
        await Navigation.PushAsync(_sendConfigPage);
    }

    private void CancelScan()
    {
        try
        {
            _scanCts?.Cancel();
        }
        catch { }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }
#endif
}
