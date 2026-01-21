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
        BackButton.IsVisible = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
            return;

        _started = true;

        if (_ctx == null || _device == null)
        {
            MainLabel.Text = "Missing provisioning data.";
            BackButton.IsVisible = true;
            return;
        }

        var success = true;
        try
        {
            await _prov.ConnectAndSetup(
                _device,
                _ctx.GroupName,
                _ctx.Ssid,
                _ctx.Password
            );
        }
        catch
        {
            success = false;
        }

        MainLabel.Text = success
            ? "Configuration sent successfully."
            : "Configuration failed. Please check WiFi credentials and try again.";

        BackButton.IsVisible = true;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
#else
    public SendConfigPage()
    {
        InitializeComponent();
        MainLabel.Text = "BLE provisioning is only supported on Android for now.";
        BackButton.IsVisible = true;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    public void SetContext(LinkedLamp.Models.ProvisioningContext ctx, object device) { }
#endif
}
