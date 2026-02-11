#if ANDROID
using Android.Provider;
using Android.Content;
using Android.Locations;
using Android.OS;
#endif
using LinkedLamp.Resources.Strings;
using LinkedLamp.Services;
using LinkedLamp.Permissions;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;
using System.Text.RegularExpressions;
using static LinkedLamp.Services.LinkedLampBLEService;

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
    private string _groupId = "";

    private readonly AppState _state;
    private readonly BackendClient _backend;
    private bool _passwordVisible;

    public ScanPage(LinkedLampBLEService prov, AppState state, BackendClient backend)
    {
        InitializeComponent();
        _prov = prov;
        _state = state;
        _backend = backend;
        _prov.Verbose = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (string.IsNullOrWhiteSpace(_state.Token))
        {
            await Navigation.PopToRootAsync();
            return;
        }
        try
        {
            var groups = await _backend.GetGroupsAsync(_state.Token);
            _state.GroupsCache = groups;
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
            return;
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
            await Navigation.PopAsync();
            return;
        }
        StartScanAndConnectProcess();
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
        TogglePasswordButton.IsVisible = false;
        GroupPicker.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        MainLabel.Text = AppResources.Scan_DetectingDevice;
        SecondaryLabel.Text = "";
        if (!await CheckPermissions())
        {
            return;
        }
        if (!await ScanAndConnect())
        {
            return;
        }
        MainLabel.Text = AppResources.Scan_SelectWifiThenPasswordAndGroup;
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
        MainLabel.Text = AppResources.Scan_MainInstruction;
        Log($"[OnScan] Scan until find and connect to best device process then request ssids list process started.");
        try
        {
            IDevice? device = await _prov.ScanUntilFindBestDeviceAsync(DeviceNameFilter, 500, _scanAndConnectCts.Token);
            MainLabel.Text = AppResources.Scan_LinkedLampDetectedConnecting;
            await _prov.ConnectAsync(device);
            MainLabel.Text = AppResources.Scan_ConnectedScanningWifi;
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
            SecondaryLabel.Text = AppResources.Scan_ErrorDuringScan;
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
            MainLabel.Text = AppResources.Scan_ConnectedScanningWifi;
            SecondaryLabel.Text = AppResources.Scan_NoWifiFound;
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
        TogglePasswordButton.IsVisible = true;
        GroupPicker.IsVisible = true;
        GroupPicker.ItemsSource = _state.GroupsCache;
        var selectedId = Preferences.Get("SelectedGroupId", "");
        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            var g = _state.GroupsCache.FirstOrDefault(x => x.Id == selectedId);
            if (g != null) GroupPicker.SelectedItem = g;
        }
        if (GroupPicker.SelectedItem == null && _state.GroupsCache.Count > 0)
        {
            GroupPicker.SelectedItem = _state.GroupsCache[0];
        }
        if (GroupPicker.SelectedItem is GroupDto g2)
        {
            _groupId = g2.Id ?? "";
            Preferences.Set("SelectedGroupId", _groupId);
        }
        StartProvisionProcessButton.IsVisible = !string.IsNullOrWhiteSpace(_groupId) && _groupId.Length > 2;
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
            SecondaryLabel.Text = AppResources.Scan_BluetoothPermissionError;
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
                    SecondaryLabel.Text = AppResources.Scan_BluetoothPermissionError;
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
                    SecondaryLabel.Text = AppResources.Scan_BluetoothPermissionError;
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
                        SecondaryLabel.Text = AppResources.Scan_BluetoothPermissionError;
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
                        SecondaryLabel.Text = AppResources.Scan_BluetoothPermissionMandatory;
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
                SecondaryLabel.Text = AppResources.Scan_PleaseActivateBluetooth;
                RetryScanAndConnectProcessButton.IsVisible = true;
                return false;
            }
        }
        catch (Exception)
        {
            SecondaryLabel.Text = AppResources.Scan_BluetoothActivationError;
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
        TogglePasswordButton.IsVisible = false;
        GroupPicker.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        _provisionCts = new CancellationTokenSource();
        ProvisionResult provisionResult;
        try
        {
            provisionResult = await _prov.ProvisionAsync(_state.Token, _ssid, _password, _groupId, _provisionCts.Token);
        }
        catch (Exception e)
        {
            SecondaryLabel.Text = AppResources.Scan_ErrorDuringProvisioning;
            RetryScanAndConnectProcessButton.IsVisible = true;
            Log($"[StartProvisionProcess] <Exception> {e.Message}.");
            return;
        }
        finally
        {
            CancelScanAndDisposeCancellationToken();
            await DisconnectDevice();
        }
        switch (provisionResult)
        {
            case ProvisionResult.CONFIG_OK:
                MainLabel.Text = AppResources.Scan_ConfigOk;
                break;
            case ProvisionResult.WIFI_FAILED:
                MainLabel.Text = AppResources.Scan_WifiFailed_Title;
                SecondaryLabel.Text = AppResources.Scan_WifiFailed_Subtitle;
                RetryScanAndConnectProcessButton.IsVisible = true;
                break;
            case ProvisionResult.CONFIG_FAILED:
                MainLabel.Text = AppResources.Scan_ConfigFailed_Title;
                SecondaryLabel.Text = AppResources.Scan_ConfigFailed_Subtitle;
                RetryScanAndConnectProcessButton.IsVisible = true;
                break;
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
    private void OnGroupSelected(object? sender, EventArgs e)
    {
        if (GroupPicker.SelectedItem is not GroupDto g) return;
        _groupId = g.Id ?? "";
        Preferences.Set("SelectedGroupId", _groupId);
        StartProvisionProcessButton.IsVisible = !string.IsNullOrWhiteSpace(_groupId) && _groupId.Length > 2;
    }
    private async void OnDeviceDisconnected()
    {
        Log($"[OnDeviceDisconnected] LinkedLamp device deconnected.");
        CancelScanAndDisposeCancellationToken();
        await DisconnectDevice();
        RetryScanAndConnectProcessButton.IsVisible = true;
        SsidPicker.IsVisible = false;
        PassEntry.IsVisible = false;
        TogglePasswordButton.IsVisible = false;
        GroupPicker.IsVisible = false;
        StartProvisionProcessButton.IsVisible = false;
        MainLabel.Text = AppResources.Scan_DetectingDevice;
        SecondaryLabel.Text = AppResources.Scan_DeviceDisconnected_Message;
    }
#if ANDROID
    private void SetSecondaryLabelToAppSettingsLink()
    {
        var linkSpan = new Span
        {
            Text = string.Format(AppResources.Scan_AppSettingsLinkText, (Build.VERSION.SdkInt < BuildVersionCodes.S ? AppResources.Scan_Location : AppResources.Scan_NearbyDevices)),
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
                        new Span { Text = AppResources.Scan_BluetoothPermissionDenied_Prefix },
                        linkSpan,
                        new Span { Text = "." },
                    }
        };
    }
    private void SetSecondaryLabelToLocationLink()
    {
        var linkSpan = new Span
        {
            Text = AppResources.Scan_LocationLinkText,
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
            new Span { Text = AppResources.Scan_LocationDisabled_Prefix },
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
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        PassEntry.IsPassword = !_passwordVisible;

        TogglePasswordButton.Source = _passwordVisible
            ? "eye_open.png"
            : "eye_closed.png";
    }
}
