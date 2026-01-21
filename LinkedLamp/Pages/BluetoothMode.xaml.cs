using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class BluetoothMode : ContentPage
{
    private readonly ScanPage _scanPage;
    private ProvisioningContext? _ctx;

    public BluetoothMode(ScanPage scanPage)
    {
        InitializeComponent();
        _scanPage = scanPage;
    }
    public void SetContext(ProvisioningContext ctx)
    {
        _ctx = ctx;
        MainLabel.Text = "Turn on the BluetoothMode on your LinkedLamp device :\n" +
            "    - Press the bouton of your LinkedLamp device and keep it pressed\n" +
            "    - Power your LinkedLamp device (still maintain the button pressed)\n" +
            "    - Wait for your LinkedLamp device to flash blue\n" +
            "    - You can release the button of your LinkedLamp device\n" +
            "    - The BlueTooth mode of your LinkedLamp device is on!";
        NextButton.Text = "Next";
    }
    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (_ctx == null)
            return;

        _scanPage.SetContext(_ctx);
        await Navigation.PushAsync(_scanPage);
    }
}
