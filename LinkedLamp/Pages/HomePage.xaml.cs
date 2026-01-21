#if ANDROID
using Android;
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using LinkedLamp.Permissions;
using LinkedLamp.Services;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif

namespace LinkedLamp.Pages;

#if ANDROID
public partial class HomePage : ContentPage
{
    public class DeviceRow : IEquatable<DeviceRow>
    {
        public string Name { get; set; } = "";
        public IDevice Device { get; set; } = default!;

        public bool Equals(DeviceRow? other)
        {
            return other != null && Device.Name == other.Device.Name;
        }
    }
    private readonly EspBleProvisioningService _prov;
    private ObservableCollection<DeviceRow> _devices = new();
    private WifiManager? _wifiManager;
    private WifiStateReceiver? _wifiReceiver;
    public HomePage(EspBleProvisioningService prov)
    {
        InitializeComponent();
        _wifiManager = null;
        _prov = prov;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DevicesView.ItemsSource = _devices;
        var context = Android.App.Application.Context;
        _wifiManager = (WifiManager?)context.GetSystemService(Context.WifiService);
        if (_wifiManager == null)
        {
            MainLabel.Text = "Problem with WiFi... Please restart the application.";
            return;
        }
        _wifiReceiver = new WifiStateReceiver();
        var filter = new IntentFilter(WifiManager.WifiStateChangedAction);
        Android.App.Application.Context.RegisterReceiver(_wifiReceiver, filter);

        if (!_wifiManager.IsWifiEnabled)
        {
            OnWifiDisabled();
        }
        else
        {
            OnWifiEnabled();
        }
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_wifiReceiver != null)
        {
            _wifiReceiver.WifiStateChanged -= OnWifiStateChanged;
            Android.App.Application.Context.UnregisterReceiver(_wifiReceiver);
            _wifiReceiver = null;
            _wifiManager = null;
        }
    }
    private void OnWifiStateChanged(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (enabled)
            {
                OnWifiEnabled();
            }
            else
            {
                OnWifiDisabled();
            }
        });
    }
    private void OnWifiDisabled()
    {
        _wifiReceiver?.WifiStateChanged += OnWifiStateChanged;
        MainLabel.Text = "Please activate the Wifi.";
        OpenWifi.IsVisible = true;
        SsidPicker.IsVisible = false;
    }
    public async void OnWifiEnabled()
    {
        _wifiReceiver?.WifiStateChanged -= OnWifiStateChanged;
        MainLabel.Text = "Select the wifi network you want your LinkedLamp to connect to.";
        OpenWifi.IsVisible = false;
        SsidPicker.IsVisible = true;
        SsidPicker.IsEnabled = false;
        var ssids = await GetNearbySsidsAsync();
        SsidPicker.ItemsSource = ssids;
        SsidPicker.IsEnabled = true;
    }

    public void OnSsidSelected(object? sender, EventArgs e)
    {
        MainLabel.Text = "Enter the wifi password";
        PassEntry.Text = "";
        PassEntry.IsVisible = true;
        SetupGroupName.IsVisible = true;
    }

    private void OnPassEntryChanged(object? sender, TextChangedEventArgs e)
    {
        SetupGroupName.IsEnabled = e.NewTextValue.Length >= 8;
    }

    private async void OnSetupGroupName(object sender, EventArgs e)
    {
        MainLabel.Text = "Enter the group name that you want to join or create.";
        SetupGroupName.IsVisible = false;
        PassEntry.IsVisible = false;
        SsidPicker.IsVisible = false;
        GroupNameEntry.IsVisible = true;
        GroupNameEntry.Text = "";
        SetupArduino.IsVisible = true;
    }
    private void OnGroupNameEntryChanged(object? sender, TextChangedEventArgs e)
    {
        SetupArduino.IsEnabled = e.NewTextValue.Length >= 1;
    }
    private async void EnsureBluetoothEnabled(object sender, EventArgs e)
    {
        MainLabel.Text = "Bluetooth activation.";
        PassEntry.IsVisible = false;
        SsidPicker.IsVisible = false;
        GroupNameEntry.IsVisible = false;
        SetupArduino.IsEnabled = false;
        var st = await MauiPermissions.RequestAsync<BluetoothScanPermission>();
        if (st != PermissionStatus.Granted)
        {
            MainLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth and retry.";
            SetupArduino.IsEnabled = true;
            return;
        }
        bool btEnabled = CrossBluetoothLE.Current.State == BluetoothState.On;
        if (!btEnabled)
        {
            btEnabled = await LinkedLamp.Platforms.Android.BluetoothEnabler.RequestEnableAsync();
        }
        if (!btEnabled)
        {
            MainLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth and retry.";
            SetupArduino.IsEnabled = true;
        }
        else
        {
            Scan();
        }
    }
    public async void Scan()
    {
        MainLabel.Text = "Detecting LinkedLamp devices around.";
        SecondaryLabel.IsVisible = false;
        SetupArduino.Text = "Scanning...";
        _devices.Clear();
        HashSet<IDevice> devices = await _prov.Scan(4);
        foreach (var device in devices)
        {
            _devices.Add(new DeviceRow() { Name = device.Name, Device = device });
        }
        SecondaryLabel.Text = _devices.Count == 0 ? "No device detected. Ensure your LinkedLamp is turned on and near of your android device." : "Select a LinkedLamp device to setup it";
        SecondaryLabel.IsVisible = true;
        DevicesView.IsVisible = true;
        SetupArduino.Text = "Scan";
        SetupArduino.IsEnabled = true;
    }
    public async void OnDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DevicesView.SelectedItem == null)
        {
            return;
        }
        SetupArduino.IsEnabled = false;
        DevicesView.IsEnabled = false;
        bool provSuccess = true;
        IDevice selectedDevice = ((DeviceRow)DevicesView.SelectedItem).Device;
        try
        {
            await _prov.ConnectAndSetup(
            selectedDevice,
            GroupNameEntry.Text ?? "",
            (string)SsidPicker.SelectedItem,
            PassEntry.Text ?? ""
        );
        }
        catch (Exception)
        {
            provSuccess = false;
        }
        SetupArduino.IsEnabled = true;
        DevicesView.IsEnabled = true;
        MainLabel.Text = provSuccess ? "Process completed. You can now setup another LinkedLamp." : "Process failed. You can try again.";
        if (!provSuccess)
        {
            DevicesView.SelectedItem = null;
        }
    }
    public async Task<List<string>> GetNearbySsidsAsync()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            await MauiPermissions.RequestAsync<MauiPermissions.NearbyWifiDevices>();
        else
            await MauiPermissions.RequestAsync<MauiPermissions.LocationWhenInUse>();

        var results = _wifiManager.ScanResults;
        if (results == null)
            return new();

        return results
            .Where(r => !string.IsNullOrWhiteSpace(r.Ssid))
            .GroupBy(r => r.Ssid)                 // un SSID peut apparaître plusieurs fois
            .Select(g => g.OrderByDescending(r => r.Level).First())
            .OrderByDescending(r => r.Level)      // RSSI fort en premier
            .Select(r => r.Ssid)
            .ToList();
    }
    public void OpenWifiSettings(object sender, EventArgs e)
    {
        var intent = new Intent(Settings.ActionWifiSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
    }
}
#endif
