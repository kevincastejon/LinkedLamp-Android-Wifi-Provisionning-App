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
            var action = await DisplayActionSheet("Account", "Cancel", null, "Change password", "Logout", "Delete account");

            if (action == "Change password")
            {
                var currentPassword = await DisplayPromptAsync("Change password", "Current password:", "Next", "Cancel", "Password", maxLength: 128);
                if (currentPassword == null) return;

                var newPassword = await DisplayPromptAsync("Change password", "New password:", "Next", "Cancel", "Password", maxLength: 128);
                if (newPassword == null) return;

                var confirmPassword = await DisplayPromptAsync("Change password", "Confirm new password:", "OK", "Cancel", "Password", maxLength: 128);
                if (confirmPassword == null) return;

                currentPassword = currentPassword.Trim();
                newPassword = newPassword.Trim();
                confirmPassword = confirmPassword.Trim();

                if (newPassword.Length < 6)
                {
                    await DisplayAlert("Change password", "New password is too short.", "OK");
                    return;
                }

                if (newPassword != confirmPassword)
                {
                    await DisplayAlert("Change password", "Passwords do not match.", "OK");
                    return;
                }

                try
                {
                    await _backend.ChangePasswordAsync(_state.Token!, currentPassword, newPassword);
                    await DisplayAlert("Change password", "Password updated.", "OK");
                }
                catch (BackendAuthException)
                {
                    _backend.ClearToken();
                    _state.GroupsCache.Clear();
                    await Navigation.PopToRootAsync();
                    UpdateUi();
                    ConfigureToolbar();
                }
                catch (BackendHttpException ex) when (ex.Code == "invalid_credentials")
                {
                    await DisplayAlert("Change password", "Current password is incorrect.", "OK");
                }
                catch (BackendHttpException ex) when (ex.Code == "new_password_too_short")
                {
                    await DisplayAlert("Change password", "New password is too short.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.Message, "OK");
                }
            }
            else if (action == "Logout")
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
        var username = await DisplayPromptAsync("Password recovery", "Enter your username:", "OK", "Cancel");
        if (string.IsNullOrWhiteSpace(username)) return;

        try
        {
            await _backend.ForgotPasswordAsync(username.Trim());
            await DisplayAlert("Password recovery", "A new password has been sent to the email associated with this account.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "email_not_set")
        {
            await DisplayAlert("Password recovery", "No email is associated with this account.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "user_not_found")
        {
            await DisplayAlert("Password recovery", "User not found.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "email_send_failed")
        {
            await DisplayAlert("Password recovery", "Email sending failed (SMTP not configured or error).", "OK");
        }
        catch
        {
            await DisplayAlert("Password recovery", "Connection failure.", "OK");
        }
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
