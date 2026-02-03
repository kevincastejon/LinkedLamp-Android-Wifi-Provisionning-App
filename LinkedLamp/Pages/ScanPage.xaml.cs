#if ANDROID
using Android.Provider;
using Android.Content;
using Android.Locations;
using Android.OS;
#endif
using LinkedLamp.Services;
using LinkedLamp.Permissions;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
using System.Text.RegularExpressions;

namespace LinkedLamp.Pages;

public partial class ScanPage : ContentPage
{
    private const string DeviceNameFilter = "LinkedLamp_Caskev_";
    private readonly LinkedLampBLEService _prov;

    private CancellationTokenSource? _scanAndConnectCts;
    private CancellationTokenSource? _provisionCts;
    private bool _verbose = true;

    private string _ssid = "";
    private string _password = "";
    private string _groupName = "";

    public ScanPage(LinkedLampBLEService prov)
    {
        InitializeComponent();
        _prov = prov;
        _prov.Verbose = true;
    }

    protected override void OnAppearing()
    {
        StartScanAndConnectProcess();
        base.OnAppearing();
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed()
    {
        CancelScanAndDisposeCancellationToken();
        using var _ = DisconnectDevice();
        return base.OnBackButtonPressed();
    }
    private void CancelScanAndDisposeCancellationToken()
    {
        if (_scanAndConnectCts != null)
        {
            _scanAndConnectCts.Cancel();
            _scanAndConnectCts.Dispose();
            _scanAndConnectCts = null;
        }
    }
    private async Task DisconnectDevice()
    {
        _prov.OnDeviceDisconnected -= OnDeviceDisconnected;
        try
        {
            await _prov.DisconnectAsync();
        }
        catch (Exception)
        {
            return;
        }
    }
    private async void OnRetryScanAndConnectProcessButtonClicked(object sender, EventArgs e)
    {
        StartScanAndConnectProcess();
    }
    private async void StartScanAndConnectProcess()
    {
        RetryScanAndConnectProcessButton.IsVisible = false;
        SsidPicker.IsVisible = false;
        PassEntry.IsVisible = false;
        GroupNameEntry.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        MainLabel.Text = "Detecting LinkedLamp device...";
        SecondaryLabel.Text = "";
        if (!await CheckPermissions())
        {
            return;
        }
        if (!await ScanAndConnect())
        {
            return;
        }
        MainLabel.Text = "Select a WiFi network then enter WiFi password and the LinkedLamp Group you want your LinkedLamp to use";
        SecondaryLabel.Text = "";
    }
    private async Task<bool> ScanAndConnect()
    {
        if (_prov.IsScanning || _prov.IsConnecting || _prov.IsConnected)
        {
            Log($"[OnScan] LinkedLampBLEService already operating.");
            return false;
        }
        _scanAndConnectCts = new CancellationTokenSource();
        List<string> ssids;
        _prov.OnDeviceDisconnected += OnDeviceDisconnected;
        MainLabel.Text = "Turn on the bluetooth mode on your LinkedLamp device by powering it on while holding the button pressed";
        Log($"[OnScan] Scan until find and connect to best device process then request ssids list process started.");
        try
        {
            IDevice? device = await _prov.ScanUntilFindBestDeviceAsync(DeviceNameFilter, 500, _scanAndConnectCts.Token);
            MainLabel.Text = "LinkedLamp detected.\nConnecting...";
            await _prov.ConnectAsync(device);
            MainLabel.Text = "Connected to LinkedLamp.\nLinkedLamp scanning for WiFi networks...";
            ssids = await _prov.RequestSsidList();
        }
        catch (System.OperationCanceledException)
        {
            Log($"[OnScan] <Exception> Scan cancelled.");
            SecondaryLabel.Text = "";
            RetryScanAndConnectProcessButton.IsVisible = true;
            await DisconnectDevice();
            return false;
        }
        catch (Exception ex)
        {
            Log($"[OnScan] <Exception> {ex.Message}.");
            SecondaryLabel.Text = "Error during scan.";
            RetryScanAndConnectProcessButton.IsVisible = true;
            await DisconnectDevice();
            return false;
        }
        finally
        {
            CancelScanAndDisposeCancellationToken();
        }
        Log($"[OnScan] Process success.");
        if (ssids.Count == 0)
        {
            Log($"[OnScan] <Exception> Ssids list is empty.");
            MainLabel.Text = "Connected to LinkedLamp.\nLinkedLamp scanning for WiFi networks...";
            SecondaryLabel.Text = "Your LinkedLamp has not found any WiFi network around.";
            RetryScanAndConnectProcessButton.IsVisible = true;
            await DisconnectDevice();
            return false;
        }
        Log($"[OnScan] Ssids list : {string.Join(',', ssids)}");
        SsidPicker.SelectedIndex = -1;
        SsidPicker.ItemsSource = ssids;
        SsidPicker.IsVisible = true;
        SsidPicker.SelectedIndex = 0;
        PassEntry.IsVisible = true;
        GroupNameEntry.IsVisible = true;
        GroupNameEntry.Text = Preferences.Get("GroupName", "");
        return true;
    }
    private async Task<bool> CheckPermissions()
    {
        PermissionStatus st;
        try
        {
            st = await MauiPermissions.CheckStatusAsync<BluetoothScanPermission>();
        }
        catch (Exception)
        {
            SecondaryLabel.Text = "Bluetooth permission error.";
            RetryScanAndConnectProcessButton.IsVisible = true;
            return false;
        }
        if (st != PermissionStatus.Granted)
        {
            bool canAskPermission;
            if (!Preferences.Get("PermissionAsked", false))
            {
                canAskPermission = true;
            }
            else
            {
                try
                {
                    canAskPermission = MauiPermissions.ShouldShowRationale<BluetoothScanPermission>();
                }
                catch (Exception)
                {
                    SecondaryLabel.Text = "Bluetooth permission error.";
                    RetryScanAndConnectProcessButton.IsVisible = true;
                    return false;
                }
            }
            if (canAskPermission)
            {
                Preferences.Set("PermissionAsked", true);
                try
                {
                    st = await MauiPermissions.RequestAsync<BluetoothScanPermission>();
                }
                catch (Exception)
                {
                    SecondaryLabel.Text = "Bluetooth permission error.";
                    RetryScanAndConnectProcessButton.IsVisible = true;
                    return false;
                }
                if (st != PermissionStatus.Granted)
                {
                    bool canAskAgain;
                    try
                    {
                        canAskAgain = MauiPermissions.ShouldShowRationale<BluetoothScanPermission>();
                    }
                    catch (Exception)
                    {
                        SecondaryLabel.Text = "Bluetooth permission error.";
                        RetryScanAndConnectProcessButton.IsVisible = true;
                        return false;
                    }
                    if (!canAskAgain)
                    {
                        RetryScanAndConnectProcessButton.IsVisible = true;
                        SetSecondaryLabelToAppSettingsLink();
                    }
                    else
                    {
                        SecondaryLabel.Text = $"Bluetooth permission is mandatory.";
                        RetryScanAndConnectProcessButton.IsVisible = true;
                    }
                    return false;
                }
            }
            else
            {
                RetryScanAndConnectProcessButton.IsVisible = true;
                SetSecondaryLabelToAppSettingsLink();
                return false;
            }
        }
#if ANDROID
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            if (!IsLocationEnabled())
            {
                RetryScanAndConnectProcessButton.IsVisible = true;
                SetSecondaryLabelToLocationLink();
                return false;
            }
        }
#endif
        try
        {
            bool btEnabled = CrossBluetoothLE.Current.State == BluetoothState.On;
            if (!btEnabled)
            {
#if ANDROID
                btEnabled = await LinkedLamp.Platforms.Android.AndroidBluetoothEnabler.RequestEnableAsync();
#endif
            }
            if (!btEnabled)
            {
                SecondaryLabel.Text = "Please activate Bluetooth.";
                RetryScanAndConnectProcessButton.IsVisible = true;
                return false;
            }
        }
        catch (Exception)
        {
            SecondaryLabel.Text = "Bluetooth activation error.";
            RetryScanAndConnectProcessButton.IsVisible = true;
            return false;
        }
        return true;
    }
    private async void OnStartProvisionProcessButtonClicked(object sender, EventArgs e)
    {
        StartProvisionProcess();
    }
    private async void StartProvisionProcess()
    {
        SsidPicker.IsVisible = false;
        PassEntry.IsVisible = false;
        GroupNameEntry.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        _provisionCts = new CancellationTokenSource();
        bool espWifiConnected;
        try
        {
            espWifiConnected = await _prov.ProvisionAsync(_ssid, _password, _groupName, _provisionCts.Token);
        }
        catch (Exception e)
        {
            SecondaryLabel.Text = "Error during provisioning.";
            RetryScanAndConnectProcessButton.IsVisible = true;
            Log($"[StartProvisionProcess] <Exception> {e.Message}.");
            return;
        }
        finally
        {
            CancelScanAndDisposeCancellationToken();
            await DisconnectDevice();
        }
        if (espWifiConnected)
        {
            MainLabel.Text = "The LinkedLamp device is connected to wifi!";
        }
        else
        {
            MainLabel.Text = "The LinkedLamp device connection to wifi failed.";
            SecondaryLabel.Text = "Wifi password may be wrong.";
            RetryScanAndConnectProcessButton.IsVisible = true;
        }
    }
    private async void OnSsidSelected(object? sender, EventArgs e)
    {
        if (SsidPicker.SelectedItem == null)
        {
            return;
        }
        _ssid = SsidPicker.SelectedItem.ToString();
        Log($"[OnSsidSelected] Ssid selected: {_ssid}");
    }
    private void OnPassEntryChanged(object sender, TextChangedEventArgs e)
    {
        string filtered = Regex.Replace(e.NewTextValue, @"[^\x20-\x7E]", "");
        if (filtered != e.NewTextValue)
        {
            ((Entry)sender).Text = filtered;
        }
        _password = filtered;
    }
    private void OnGroupNameChanged(object sender, TextChangedEventArgs e)
    {
        string filtered = Regex.Replace(e.NewTextValue, @"[^\x20-\x7E]", "");
        if (filtered != e.NewTextValue)
        {
            ((Entry)sender).Text = filtered;
        }
        _groupName = filtered;
        Preferences.Set("GroupName", _groupName);
        StartProvisionProcessButton.IsVisible = e.NewTextValue.Length > 2;
    }
    private async void OnDeviceDisconnected()
    {
        Log($"[OnDeviceDisconnected] LinkedLamp device deconnected.");
        CancelScanAndDisposeCancellationToken();
        await DisconnectDevice();
        RetryScanAndConnectProcessButton.IsVisible = true;
        SsidPicker.IsVisible = false;
        PassEntry.IsVisible = false;
        GroupNameEntry.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        MainLabel.Text = "Detecting LinkedLamp device...";
        SecondaryLabel.Text = "Your LinkedLamp device has disconnected.";
    }
#if ANDROID
    private void SetSecondaryLabelToAppSettingsLink()
    {
        var linkSpan = new Span
        {
            Text = $"activate the permission {(Build.VERSION.SdkInt < BuildVersionCodes.S ? "Location" : "Nearby Devices")} in the app settings",
            TextColor = Colors.Cyan,
            TextDecorations = TextDecorations.Underline
        };

        linkSpan.GestureRecognizers.Add(
            new TapGestureRecognizer
            {
                Command = new Command(() => AppInfo.ShowSettingsUI())
            });

        SecondaryLabel.FormattedText = new FormattedString
        {
            Spans =
                    {
                        new Span { Text = "Bluetooth permission denied. Please " },
                        linkSpan,
                        new Span { Text = "." },
                    }
        };
    }
    private void SetSecondaryLabelToLocationLink()
    {
        var linkSpan = new Span
        {
            Text = "activate the location",
            TextColor = Colors.Cyan,
            TextDecorations = TextDecorations.Underline
        };

        linkSpan.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => OpenLocationSettings())
        });

        SecondaryLabel.FormattedText = new FormattedString
        {
            Spans =
        {
            new Span { Text = "System loation disabled. Please " },
            linkSpan,
            new Span { Text = "." },
        }
        };
    }
    private static bool IsLocationEnabled()
    {
        var context = Android.App.Application.Context;
        var lm = (LocationManager?)context.GetSystemService(Context.LocationService);
        if (lm is null) return false;

        return lm.IsProviderEnabled(LocationManager.GpsProvider)
            || lm.IsProviderEnabled(LocationManager.NetworkProvider);
    }
    private static void OpenLocationSettings()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        var intent = new Intent(Settings.ActionLocationSourceSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        activity.StartActivity(intent);
    }
#else
    private void SetSecondaryLabelToAppSettingsLink()
    {
        throw new NotImplementedException();
    }
    public static bool IsLocationEnabled()
    {
        throw new NotImplementedException();
    }
    public static void OpenLocationSettings()
    {
        throw new NotImplementedException();
    }
#endif
    private void Log(string message)
    {
        if (_verbose)
        {
            System.Diagnostics.Debug.WriteLine($"[LinkedLamp] [ScanPage] {message}");
        }
    }
}
