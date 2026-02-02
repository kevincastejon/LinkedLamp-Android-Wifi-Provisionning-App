#if ANDROID
using LinkedLamp.Permissions;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Android.Locations;
using Android.Content;
using Android.Provider;
using Android.OS;
#endif
using MauiPermissions = Microsoft.Maui.ApplicationModel.Permissions;

namespace LinkedLamp.Pages;

public partial class PermissionsPage : ContentPage
{
#if ANDROID
    private readonly ScanPage _scanPage;

    public PermissionsPage(ScanPage scanPage)
    {
        InitializeComponent();
        _scanPage = scanPage;
    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();
        StartProcessButton.IsVisible = false;
        OnStartProcessButtonClicked(new(), new());
        //PermissionStatus st;
        //try
        //{
        //    st = await MauiPermissions.CheckStatusAsync<BluetoothScanPermission>();
        //}
        //catch (Exception)
        //{
        //    SecondaryLabel.Text = "Bluetooth permission error.";
        //    StartProcessButton.IsVisible = true;
        //    return;
        //}
        //if (st != PermissionStatus.Granted)
        //{
        //    bool canAskPermission;
        //    if (!Preferences.Get("PermissionAsked", false))
        //    {
        //        canAskPermission = true;
        //    }
        //    else
        //    {
        //        try
        //        {
        //            canAskPermission = MauiPermissions.ShouldShowRationale<BluetoothScanPermission>();
        //        }
        //        catch (Exception)
        //        {
        //            SecondaryLabel.Text = "Bluetooth permission error.";
        //            return;
        //        }
        //    }
        //    if (canAskPermission)
        //    {
        //        SecondaryLabel.Text = $"";
        //    }
        //    else
        //    {
        //        SecondaryLabel.Text = $"Please activate permission in the app settings.";
        //    }
        //}
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
    }
    private async void OnStartProcessButtonClicked(object sender, EventArgs e)
    {
        StartProcessButton.IsVisible = false;
        PermissionStatus st;
        try
        {
            st = await MauiPermissions.CheckStatusAsync<BluetoothScanPermission>();
        }
        catch (Exception)
        {
            SecondaryLabel.Text = "Bluetooth permission error.";
            StartProcessButton.IsVisible = true;
            return;
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
                    StartProcessButton.IsVisible = true;
                    return;
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
                    StartProcessButton.IsVisible = true;
                    return;
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
                        StartProcessButton.IsVisible = true;
                        return;
                    }
                    if (!canAskAgain)
                    {
                        SetSecondaryLabelToAppSettingsLink();
                    }
                    else
                    {
                        SecondaryLabel.Text = $"Bluetooth permission is mandatory.";
                        StartProcessButton.IsVisible = true;
                    }
                    return;
                }
            }
            else
            {
                SetSecondaryLabelToAppSettingsLink();
                return;
            }
        }
        if (Build.VERSION.SdkInt < BuildVersionCodes.S)
        {
            if (!IsLocationEnabled())
            {
                SetSecondaryLabelToLocationLink();
                return;
            }
        }
        try
        {
            bool btEnabled = CrossBluetoothLE.Current.State == BluetoothState.On;
            if (!btEnabled)
            {
                btEnabled = await Platforms.Android.AndroidBluetoothEnabler.RequestEnableAsync();
            }
            if (!btEnabled)
            {
                SecondaryLabel.Text = "Please activate Bluetooth.";
                StartProcessButton.IsVisible = true;
                return;
            }
        }
        catch (Exception)
        {
            SecondaryLabel.Text = "Bluetooth activation error.";
            StartProcessButton.IsVisible = true;
            return;
        }
        GoToNextPage();
    }

    private void SetSecondaryLabelToLocationLink()
    {
#if ANDROID
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
#endif
    }
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

    private async void GoToNextPage()
    {
        await Navigation.PushAsync(_scanPage);
    }
    public static bool IsLocationEnabled()
    {
        var context = Android.App.Application.Context;
        var lm = (LocationManager?)context.GetSystemService(Context.LocationService);
        if (lm is null) return false;

        return lm.IsProviderEnabled(LocationManager.GpsProvider)
            || lm.IsProviderEnabled(LocationManager.NetworkProvider);
    }

    public static void OpenLocationSettings()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null) return;

        var intent = new Intent(Settings.ActionLocationSourceSettings);
        intent.AddFlags(ActivityFlags.NewTask);
        activity.StartActivity(intent);
    }
#endif
}
