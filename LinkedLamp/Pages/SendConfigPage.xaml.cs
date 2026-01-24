#if ANDROID
using LinkedLamp.Models;
using Plugin.BLE.Abstractions.Contracts;
#endif

namespace LinkedLamp.Pages;

public partial class SendConfigPage : ContentPage
{
#if ANDROID
    private readonly LinkedLamp.Services.EspBleProvisioningService _prov;
    private ProvisioningContext? _ctx;
    private IDevice? _device;
    private bool _started;
    private CancellationTokenSource? _provCts;

    public SendConfigPage(LinkedLamp.Services.EspBleProvisioningService prov)
    {
        InitializeComponent();
        _prov = prov;
    }

    public void SetContext(ProvisioningContext ctx, IDevice device)
    {
        _ctx = ctx;
        _device = device;
        _started = false;
        MainLabel.Text = "Sending configuration...";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
            return;

        _started = true;
        _ = RunProvisioningAsync();
    }

    protected override void OnDisappearing()
    {
        try { _provCts?.Cancel(); } catch { }
        _provCts?.Dispose();
        _provCts = null;
        base.OnDisappearing();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try { _provCts?.Cancel(); } catch { }
        _provCts?.Dispose();
        _provCts = null;
        await Navigation.PopAsync();
    }
    private async Task RunProvisioningAsync()
    {
        if (_ctx == null || _device == null)
        {
            MainLabel.Text = "Missing provisioning data.";
            return;
        }
        MainLabel.Text = "Sending configuration...";

        _provCts?.Cancel();
        _provCts?.Dispose();
        _provCts = new CancellationTokenSource();

        bool success;
        try
        {
            await _prov.ConnectAndSetup(
                _device,
                _ctx.GroupName,
                _ctx.Ssid,
                _ctx.Password,
                _provCts.Token);

            success = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            success = false;
        }

        MainLabel.Text = success
            ? "Configuration sent successfully."
            : "Configuration failed. Please check WiFi credentials and try again.";
    }
#endif
}
