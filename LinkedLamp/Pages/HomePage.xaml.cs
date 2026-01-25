namespace LinkedLamp.Pages;

public partial class HomePage : ContentPage
{
    private readonly WifiSsidPage _wifiPage;

    public HomePage(WifiSsidPage wifiPage)
    {
        InitializeComponent();
        _wifiPage = wifiPage;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_wifiPage);
    }
}
