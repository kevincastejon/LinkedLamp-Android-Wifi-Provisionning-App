#if ANDROID
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
#endif
using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class WifiPage : ContentPage
{
    private readonly GroupPage _groupPage;
    private readonly ProvisioningContext _ctx;

#if ANDROID
    private WifiManager? _wifiManager;
    private WifiStateReceiver? _wifiReceiver;
#endif

    public WifiPage(GroupPage groupPage)
    {
        InitializeComponent();
        _groupPage = groupPage;
        _ctx = new ProvisioningContext();
#if ANDROID
        _wifiManager = null;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MainLabel.Text = "Select the WiFi network you want your LinkedLamp to connect to";
        PassEntry.IsVisible = false;
        NextButton.IsVisible = false;
        NextButton.IsEnabled = false;

#if ANDROID
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
            await OnWifiEnabledAsync();
        }
#else
        MainLabel.Text = "WiFi selection is only supported on Android for now.";
        SsidPicker.IsVisible = false;
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        if (_wifiReceiver != null)
        {
            _wifiReceiver.WifiStateChanged -= OnWifiStateChanged;
            Android.App.Application.Context.UnregisterReceiver(_wifiReceiver);
            _wifiReceiver = null;
            _wifiManager = null;
        }
#endif
    }

#if ANDROID
    private void OnWifiStateChanged(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (enabled)
            {
                await OnWifiEnabledAsync();
            }
            else
            {
                OnWifiDisabled();
            }
        });
    }

    private void OnWifiDisabled()
    {
        if (_wifiReceiver != null)
            _wifiReceiver.WifiStateChanged += OnWifiStateChanged;

        MainLabel.Text = "Please activate the Wifi.";
        OpenWifi.IsVisible = true;
        SsidPicker.IsVisible = false;
        PassEntry.IsVisible = false;
        NextButton.IsVisible = false;
    }

    private async Task OnWifiEnabledAsync()
    {
        if (_wifiReceiver != null)
            _wifiReceiver.WifiStateChanged -= OnWifiStateChanged;

        MainLabel.Text = "Select the WiFi network you want your LinkedLamp to connect to";
        OpenWifi.IsVisible = false;
        SsidPicker.IsVisible = true;
        SsidPicker.IsEnabled = false;

        var ssids = await GetNearbySsidsAsync();
        SsidPicker.ItemsSource = ssids;
        SsidPicker.IsEnabled = true;
    }

    private async Task<List<string>> GetNearbySsidsAsync()
    {
        if (_wifiManager == null)
            return new();

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            await MauiPermissions.RequestAsync<MauiPermissions.NearbyWifiDevices>();
        else
            await MauiPermissions.RequestAsync<MauiPermissions.LocationWhenInUse>();

        var results = _wifiManager.ScanResults;
        if (results == null)
            return new();

        return results
            .Where(r => !string.IsNullOrWhiteSpace(r.Ssid))
            .GroupBy(r => r.Ssid)
            .Select(g => g.OrderByDescending(r => r.Level).First())
            .OrderByDescending(r => r.Level)
            .Select(r => r.Ssid)
            .ToList();
    }

    public void OpenWifiSettings(object sender, EventArgs e)
    {
        var intent = new Intent(Settings.ActionWifiSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        Android.App.Application.Context.StartActivity(intent);
    }
#endif

    private void OnSsidSelected(object? sender, EventArgs e)
    {
        if (SsidPicker.SelectedItem == null)
            return;

        _ctx.Ssid = (string)SsidPicker.SelectedItem;

        MainLabel.Text = "Enter the password for the selected WiFi network";
        PassEntry.Text = "";
        PassEntry.IsVisible = true;

        NextButton.IsVisible = true;
        NextButton.IsEnabled = false;
    }

    private void OnPassEntryChanged(object? sender, TextChangedEventArgs e)
    {
        _ctx.Password = e.NewTextValue ?? "";
        NextButton.IsEnabled = _ctx.Password.Length > 8;
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        _groupPage.SetContext(_ctx);
        await Navigation.PushAsync(_groupPage);
    }
}
