using LinkedLamp.Services;

namespace LinkedLamp.Pages;

public partial class HomePage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;
    private readonly ScanPage _scanPage;
    private readonly ManageGroupsPage _manageGroupsPage;
    private readonly RegisterPage _registerPage;
    private readonly LoginPage _loginPage;

    public HomePage(AppState state, BackendClient backend, ScanPage scanPage, ManageGroupsPage manageGroupsPage, RegisterPage registerPage, LoginPage loginPage)
    {
        InitializeComponent();
        _state = state;
        _backend = backend;
        _scanPage = scanPage;
        _manageGroupsPage = manageGroupsPage;
        _registerPage = registerPage;
        _loginPage = loginPage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _backend.LoadTokenAsync();
        UpdateUi();
        ConfigureToolbar();
    }

    private void UpdateUi()
    {
        var hasToken = !string.IsNullOrWhiteSpace(_state.Token);
        LoggedInLayout.IsVisible = hasToken;
        LoggedOutLayout.IsVisible = !hasToken;
    }

    private void ConfigureToolbar()
    {
        ToolbarItems.Clear();
        if (string.IsNullOrWhiteSpace(_state.Token)) return;

        var menu = new ToolbarItem { Text = "Menu", Order = ToolbarItemOrder.Primary, Priority = 0 };
        menu.Clicked += async (_, __) =>
        {
            var action = await DisplayActionSheet("Account", "Cancel", null, "Logout", "Delete account");
            if (action == "Logout")
            {
                _backend.ClearToken();
                _state.GroupsCache.Clear();
                await Navigation.PopToRootAsync();
                UpdateUi();
                ConfigureToolbar();
            }
            else if (action == "Delete account")
            {
                var confirm = await DisplayAlert("Delete account", "This will delete your account and all owned groups. Continue?", "Delete", "Cancel");
                if (!confirm) return;

                try
                {
                    await _backend.DeleteAccountAsync(_state.Token!);
                }
                catch (BackendAuthException)
                {
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                    return;
                }

                _backend.ClearToken();
                _state.GroupsCache.Clear();
                await Navigation.PopToRootAsync();
                UpdateUi();
                ConfigureToolbar();
            }
        };

        ToolbarItems.Add(menu);
    }

    private async void OnCreateAccountClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_registerPage);
    }

    private async void OnConnectToAccountClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_loginPage);
    }

    private async void OnForgotPasswordClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Not implemented", "Password recovery is not implemented yet.", "OK");
    }

    private async void OnManageGroupsClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_manageGroupsPage);
    }

    private async void OnConfigureLinkedLampClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_scanPage);
    }
}
