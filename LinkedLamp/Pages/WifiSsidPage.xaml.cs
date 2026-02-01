#if ANDROID
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif
using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class WifiSsidPage : ContentPage
{
    private readonly WifiPassPage _wifiPassPage;
    private readonly ProvisioningContext _ctx;

#if ANDROID
    private WifiManager? _wifiManager;
    private WifiStateReceiver? _wifiReceiver;
#endif

    public WifiSsidPage(WifiPassPage wifiPassPage)
    {
        InitializeComponent();
        _wifiPassPage = wifiPassPage;
        _ctx = new ProvisioningContext();
#if ANDROID
        _wifiManager = null;
#endif
    }

//    protected override async void OnAppearing()
//    {
//        base.OnAppearing();
//        SsidPicker.IsVisible = false;
//        SsidPicker.SelectedItem = null;
//        SsidPicker.ItemsSource = null;
//#if ANDROID
//        var context = Android.App.Application.Context;
//        _wifiManager = (WifiManager?)context.GetSystemService(Context.WifiService);
//        if (_wifiManager == null)
//        {
//            MainLabel.Text = "Problem with WiFi... Please restart the application.";
//            return;
//        }

//        _wifiReceiver = new WifiStateReceiver();
//        var filter = new IntentFilter(WifiManager.WifiStateChangedAction);
//        Android.App.Application.Context.RegisterReceiver(_wifiReceiver, filter);

//        if (!_wifiManager.IsWifiEnabled)
//        {
//            OnWifiDisabled();
//        }
//        else
//        {
//            await OnWifiEnabledAsync();
//        }
//#endif
//    }

//    protected override void OnDisappearing()
//    {
//        base.OnDisappearing();
//#if ANDROID
//        if (_wifiReceiver != null)
//        {
//            _wifiReceiver.WifiStateChanged -= OnWifiStateChanged;
//            Android.App.Application.Context.UnregisterReceiver(_wifiReceiver);
//            _wifiReceiver = null;
//            _wifiManager = null;
//        }
//#endif
//    }

//#if ANDROID
//    private void OnWifiStateChanged(bool enabled)
//    {
//        MainThread.BeginInvokeOnMainThread(async () =>
//        {
//            if (enabled)
//            {
//                await OnWifiEnabledAsync();
//            }
//            else
//            {
//                OnWifiDisabled();
//            }
//        });
//    }

//    private void OnWifiDisabled()
//    {
//        if (_wifiReceiver != null)
//            _wifiReceiver.WifiStateChanged += OnWifiStateChanged;

//        MainLabel.Text = "Please activate the Wifi.";
//        OpenWifi.IsVisible = true;
//        SsidPicker.IsVisible = false;
//    }

//    private async Task OnWifiEnabledAsync()
//    {
//        if (_wifiReceiver != null)
//            _wifiReceiver.WifiStateChanged -= OnWifiStateChanged;


//        MainLabel.Text = "Select the WiFi network you want your LinkedLamp to connect to";
//        OpenWifi.IsVisible = false;
//        SsidPicker.IsVisible = true;
//        SsidPicker.IsEnabled = false;

//        var ssids = await GetNearbySsidsAsync();
//        SsidPicker.ItemsSource = ssids;
//        SsidPicker.IsEnabled = true;
//    }

//    private async Task<List<string>> GetNearbySsidsAsync()
//    {
//        if (_wifiManager == null)
//            return new();

//        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
//            await MauiPermissions.RequestAsync<MauiPermissions.NearbyWifiDevices>();
//        else
//            await MauiPermissions.RequestAsync<MauiPermissions.LocationWhenInUse>();

//        var results = _wifiManager.ScanResults;
//        if (results == null)
//            return new();

//        return results
//            .Where(r => !string.IsNullOrWhiteSpace(r.Ssid))
//            .GroupBy(r => r.Ssid)
//            .Select(g => g.OrderByDescending(r => r.Level).First())
//            .OrderByDescending(r => r.Level)
//            .Select(r => r.Ssid)
//            .ToList();
//    }
//#endif

    private async void OnSsidSelected(object? sender, EventArgs e)
    {
        if (SsidPicker.SelectedItem == null)
            return;

        _ctx.Ssid = (string)SsidPicker.SelectedItem;
        _wifiPassPage.SetContext(_ctx);
        await Navigation.PushAsync(_wifiPassPage);
    }
}
