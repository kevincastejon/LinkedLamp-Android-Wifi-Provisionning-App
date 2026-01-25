using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class WifiPassPage : ContentPage
{
    private readonly GroupPage _groupPage;
    private ProvisioningContext _ctx;

    public WifiPassPage(GroupPage groupPage)
    {
        InitializeComponent();
        _groupPage = groupPage;
        _ctx = new ProvisioningContext();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        NextButton.IsVisible = true;
        NextButton.IsEnabled = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private void OnPassEntryChanged(object? sender, TextChangedEventArgs e)
    {
        _ctx.Password = e.NewTextValue ?? "";
        NextButton.IsEnabled = _ctx.Password.Length > 4;
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        _groupPage.SetContext(_ctx);
        await Navigation.PushAsync(_groupPage);
        Navigation.RemovePage(this);
    }
    public void SetContext(ProvisioningContext ctx)
    {
        _ctx = ctx;
        SsidLabel.Text = _ctx.Ssid;
        PassEntry.Text = "";
        NextButton.IsEnabled = false;
    }
}
