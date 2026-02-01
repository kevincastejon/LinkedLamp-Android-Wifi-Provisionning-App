#if ANDROID
using LinkedLamp.Permissions;
using LinkedLamp.Services;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;
using System.Threading;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif

using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class ScanPage : ContentPage
{
#if ANDROID
    private const string DeviceNameFilter = "LinkedLamp_Caskev_";
    private readonly LinkedLampBLEService _prov;
    private readonly WifiSsidPage _wifiSsidPage;

    private CancellationTokenSource? _scanCts;

    public ScanPage(LinkedLampBLEService prov, WifiSsidPage wifiSsidPage)
    {
        InitializeComponent();
        _prov = prov;
        _wifiSsidPage = wifiSsidPage;
    }

    protected override void OnAppearing()
    {
        ScanButton.IsEnabled = true;
        SecondaryLabel.Text = "";
        base.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        CancelScanAndDisposeCancellationToken();
        base.OnDisappearing();
    }

    private void CancelScanAndDisposeCancellationToken()
    {
        if (_scanCts != null)
        {
            _scanCts.Cancel();
            _scanCts.Dispose();
            _scanCts = null;
        }
    }

    private async void OnScanButtonClicked(object sender, EventArgs e)
    {
        if (_prov.IsScanning || _prov.IsConnecting || _prov.IsConnected)
        {
            Debug.WriteLine("[LinkedLamp] [ScanPage] [OnScan] LinkedLampBLEService already operating.");
            return;
        }
        ScanButton.IsEnabled = false;
        _scanCts = new CancellationTokenSource();
        List<string> ssids;
        Debug.WriteLine("[LinkedLamp] [ScanPage] [OnScan] Scan until find and connect to best device process then request ssids list process started");
        try
        {
           ssids = await _prov.ScanUntilFindAndConnectToBestDeviceThenRequestSsidsListAsync(DeviceNameFilter, 500, _scanCts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[LinkedLamp] [ScanPage] [OnScan] <Exception> Scan cancelled.");
            CancelScanAndDisposeCancellationToken();
            ScanButton.IsEnabled = true;
            SecondaryLabel.Text = "";
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LinkedLamp] [ScanPage] [OnScan] <Exception> {ex.Message}");
            CancelScanAndDisposeCancellationToken();
            ScanButton.IsEnabled = true;
            SecondaryLabel.Text = "Error during scan.";
            return;
        }
        finally
        {
            CancelScanAndDisposeCancellationToken();
            ScanButton.IsEnabled = true;
        }
        Debug.WriteLine("[LinkedLamp] [ScanPage] [OnScan] Process success");
        Debug.WriteLine($"[LinkedLamp] [ScanPage] [OnScan] Ssids list : {string.Join(',', ssids)}");
        SecondaryLabel.Text = "";
        await Navigation.PushAsync(_wifiSsidPage);
    }
#endif
}
