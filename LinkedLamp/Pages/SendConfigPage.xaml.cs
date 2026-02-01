#if ANDROID
using LinkedLamp.Models;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;
#endif

namespace LinkedLamp.Pages;

public partial class SendConfigPage : ContentPage
{
#if ANDROID
    private readonly LinkedLamp.Services.LinkedLampBLEService _prov;
    private readonly IServiceProvider _services;

    private ProvisioningContext? _ctx;
    private IDevice? _device;

    private CancellationTokenSource? _cts;
    private Task? _task;
    private bool _started;

    public SendConfigPage(LinkedLamp.Services.LinkedLampBLEService prov, IServiceProvider services)
    {
        InitializeComponent();
        _prov = prov;
        _services = services;
    }

    public void SetContext(ProvisioningContext ctx, IDevice device)
    {
        _ctx = ctx;
        _device = device;
        _started = false;

        SecondaryLabel.Text = "Sending configuration...";
        BackWifiButton.IsVisible = false;
        BackHomeButton.IsVisible = false;
    }

    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();

    //    BackWifiButton.IsVisible = false;
    //    BackHomeButton.IsVisible = false;

    //    if (_started)
    //        return;

    //    _started = true;

    //    _cts?.Cancel();
    //    _cts?.Dispose();
    //    _cts = new CancellationTokenSource();

    //    _task = RunAsync(_cts.Token);
    //}

    //protected override void OnDisappearing()
    //{
    //    Debug.WriteLine(">>> SendConfigPage OnDisappearing.");
    //    try { _cts?.Cancel(); } catch { }
    //    _cts?.Dispose();
    //    _cts = null;

    //    _ = _prov.CancelAndDisconnectAsync();
    //    base.OnDisappearing();
    //}

    //protected override bool OnBackButtonPressed()
    //{
    //    try { _cts?.Cancel(); } catch { }
    //    _ = _prov.CancelAndDisconnectAsync();
    //    return base.OnBackButtonPressed();
    //}

    //private async Task RunAsync(CancellationToken token)
    //{
    //    if (_ctx == null || _device == null)
    //    {
    //        SecondaryLabel.Text = "Missing provisioning data.";
    //        BackWifiButton.IsVisible = true;
    //        return;
    //    }

    //    try
    //    {
    //        SecondaryLabel.Text = "Sending configuration...";

    //        await _prov.ProvisionAsync(
    //            _device,
    //            _ctx.GroupName,
    //            _ctx.Ssid,
    //            _ctx.Password,
    //            token
    //        );

    //        await MainThread.InvokeOnMainThreadAsync(() =>
    //        {
    //            SecondaryLabel.Text = "Configuration sent successfully.";
    //            BackWifiButton.IsVisible = false;
    //            BackHomeButton.IsVisible = true;
    //        });
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        Debug.WriteLine(">>> Provisioning cancelled.");
    //    }
    //    catch
    //    {
    //        await MainThread.InvokeOnMainThreadAsync(() =>
    //        {
    //            SecondaryLabel.Text = "Configuration failed. Please check WiFi credentials and try again.";
    //            BackWifiButton.IsVisible = true;
    //            BackHomeButton.IsVisible = false;
    //        });
    //    }
    //    finally
    //    {
    //        _ = _prov.CancelAndDisconnectAsync();
    //    }
    //}

    private async void OnBackWifiClicked(object sender, EventArgs e)
    {
        //try { _cts?.Cancel(); } catch { }
        //_ = _prov.CancelAndDisconnectAsync();

        //var homePage = _services.GetRequiredService<HomePage>();
        //var wifiPage = _services.GetRequiredService<WifiSsidPage>();

        //await MainThread.InvokeOnMainThreadAsync(async () =>
        //{
        //    var window = this.Window ?? Application.Current!.Windows[0];
        //    window.Page = new NavigationPage(homePage);
        //    await window.Page.Navigation.PushAsync(wifiPage, false);
        //});
    }

    private async void OnBackHomeClicked(object sender, EventArgs e)
    {
        //try { _cts?.Cancel(); } catch { }
        //_ = _prov.CancelAndDisconnectAsync();

        //var homePage = _services.GetRequiredService<HomePage>();

        //await MainThread.InvokeOnMainThreadAsync(() =>
        //{
        //    var window = this.Window ?? Application.Current!.Windows[0];
        //    window.Page = new NavigationPage(homePage);
        //});
    }
#endif
}
