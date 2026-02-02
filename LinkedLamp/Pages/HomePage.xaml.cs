namespace LinkedLamp.Pages;

public partial class HomePage : ContentPage
{
    //private readonly WifiSsidPage _wifiPage;
    private readonly ScanPage _scanPage;

    public HomePage(ScanPage scanPage)
    {
        InitializeComponent();
        _scanPage = scanPage;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_scanPage);
    }
}
