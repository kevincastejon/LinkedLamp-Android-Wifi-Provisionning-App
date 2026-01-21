namespace LinkedLamp.Pages;

public partial class HomePage : ContentPage
{
    private readonly WifiPage _wifiPage;

    public HomePage(WifiPage wifiPage)
    {
        InitializeComponent();
        _wifiPage = wifiPage;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_wifiPage);
    }
}
