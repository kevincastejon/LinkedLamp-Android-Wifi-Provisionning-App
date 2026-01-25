using LinkedLamp.Models;

namespace LinkedLamp.Pages;

public partial class GroupPage : ContentPage
{
    private readonly ScanPage _scanPage;
    private ProvisioningContext? _ctx;

    public GroupPage(ScanPage scanPage)
    {
        InitializeComponent();
        _scanPage = scanPage;
    }

    public void SetContext(ProvisioningContext ctx)
    {
        _ctx = ctx;
        GroupNameEntry.Text = "";
        NextButton.IsEnabled = false;
    }

    private void OnGroupNameChanged(object sender, TextChangedEventArgs e)
    {
        if (_ctx == null)
            return;

        _ctx.GroupName = e.NewTextValue ?? "";
        NextButton.IsEnabled = _ctx.GroupName.Length > 1;
    }

    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (_ctx == null)
            return;
        _scanPage.SetContext(_ctx);
        await Navigation.PushAsync(_scanPage);
    }
}
