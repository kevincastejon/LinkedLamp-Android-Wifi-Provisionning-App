using LinkedLamp.Resources.Strings;
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

        var menu = new ToolbarItem { Text = AppResources.Home_AccountHamburgerMenu_Title, Order = ToolbarItemOrder.Primary, Priority = 0 };
        menu.Clicked += async (_, __) =>
        {
            var action = await DisplayActionSheetAsync(AppResources.Home_AccountHamburgerMenu_Title, AppResources.Global_Cancel, null, AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_Logout, AppResources.Home_AccountHamburgerMenu_DeleteAccount);

            if (action == AppResources.Home_AccountHamburgerMenu_ChangePassword)
            {
                var currentPassword = await DisplayPromptAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_CurrentPassword, AppResources.Global_Next, AppResources.Global_Cancel, AppResources.Global_Password, maxLength: 128);
                if (currentPassword == null) return;

                var newPassword = await DisplayPromptAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_NewPassword, AppResources.Global_Next, AppResources.Global_Cancel, AppResources.Global_Password, maxLength: 128);
                if (newPassword == null) return;

                var confirmPassword = await DisplayPromptAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_ConfirmNewPassword, "OK", AppResources.Global_Cancel, AppResources.Global_Password, maxLength: 128);
                if (confirmPassword == null) return;

                currentPassword = currentPassword.Trim();
                newPassword = newPassword.Trim();
                confirmPassword = confirmPassword.Trim();

                if (newPassword.Length < 6)
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, "New password is too short.", "OK");
                    return;
                }

                if (newPassword != confirmPassword)
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, "Passwords do not match.", "OK");
                    return;
                }

                try
                {
                    await _backend.ChangePasswordAsync(_state.Token!, currentPassword, newPassword);
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, "Password updated.", "OK");
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
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, "Current password is incorrect.", "OK");
                }
                catch (BackendHttpException ex) when (ex.Code == "new_password_too_short")
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, "New password is too short.", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync("Error", ex.Message, "OK");
                }
            }
            else if (action == AppResources.Home_AccountHamburgerMenu_Logout)
            {
                _backend.ClearToken();
                _state.GroupsCache.Clear();
                await Navigation.PopToRootAsync();
                UpdateUi();
                ConfigureToolbar();
            }
            else if (action == AppResources.Home_AccountHamburgerMenu_DeleteAccount)
            {
                var confirm = await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_DeleteAccount, "This will delete your account and all owned groups. Continue?", "Delete", AppResources.Global_Cancel);
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
                    await DisplayAlertAsync("Error", ex.Message, "OK");
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
        var username = await DisplayPromptAsync("Password recovery", "Enter your username:", "OK", AppResources.Global_Cancel);
        if (string.IsNullOrWhiteSpace(username)) return;

        try
        {
            await _backend.ForgotPasswordAsync(username.Trim());
            await DisplayAlertAsync("Password recovery", "A new password has been sent to the email associated with this account.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "email_not_set")
        {
            await DisplayAlertAsync("Password recovery", "No email is associated with this account.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "user_not_found")
        {
            await DisplayAlertAsync("Password recovery", "User not found.", "OK");
        }
        catch (BackendHttpException ex) when (ex.Code == "email_send_failed")
        {
            await DisplayAlertAsync("Password recovery", "Email sending failed (SMTP not configured or error).", "OK");
        }
        catch
        {
            await DisplayAlertAsync("Password recovery", "Connection failure.", "OK");
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
