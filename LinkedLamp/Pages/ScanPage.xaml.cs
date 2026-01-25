#if ANDROID
using LinkedLamp.Permissions;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif
using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class ScanPage : ContentPage
{
    private ProvisioningContext? _ctx;
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
    private const string DeviceNameFilter = "LinkedLamp_Caskev_";
    private readonly LinkedLamp.Services.EspBleProvisioningService _prov;
    private readonly SendConfigPage _sendConfigPage;
    private CancellationTokenSource? _scanCts;
    private readonly SemaphoreSlim _scanGate = new(1, 1);

    public ScanPage(LinkedLamp.Services.EspBleProvisioningService prov, SendConfigPage sendConfigPage)
    {
        InitializeComponent();
        _prov = prov;
        _sendConfigPage = sendConfigPage;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScanLoop();
    }
    protected override void OnDisappearing()
    {
        CancelScan();
        base.OnDisappearing();
    }

    private async void ScanLoop()
    {
        bool deviceFound = false;
        try
        {
            deviceFound = await StartScanAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!deviceFound)
        {
            await Task.Delay(1000);
            ScanLoop();
        }
    }

    private async Task<bool> StartScanAsync()
    {
        if (_ctx == null)
            return false;

        await _scanGate.WaitAsync();
        HashSet<IDevice> devices;
        try
        {
            CancelScan();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            var st = await MauiPermissions.RequestAsync<BluetoothScanPermission>();
            if (st != PermissionStatus.Granted)
            {
                SecondaryLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth.";
                return false;
            }

            var btEnabled = CrossBluetoothLE.Current.State == BluetoothState.On;
            if (!btEnabled)
            {
                btEnabled = await LinkedLamp.Platforms.Android.BluetoothEnabler.RequestEnableAsync();
            }

            if (!btEnabled)
            {
                SecondaryLabel.Text = "Bluetooth is required to setup your LinkedLamp. Please enable Bluetooth.";
                return false;
            }
            try
            {
                SecondaryLabel.Text = "Detecting...";
                devices = await _prov.Scan(DeviceNameFilter, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SecondaryLabel.Text = $"Scan failed: {ex.Message}";
                return false;
            }
        }
        finally
        {
            _scanGate.Release();
        }
        if (devices.Count == 0)
        {
            return false;
        }
        else
        {
            CancelScan();
            _sendConfigPage.SetContext(_ctx, devices.ElementAt(0));
            await Navigation.PushAsync(_sendConfigPage);
            Navigation.RemovePage(this);
            return true;
        }
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
    public void SetContext(ProvisioningContext ctx)
    {
        _ctx = ctx;
        SecondaryLabel.Text = "";
    }
}
