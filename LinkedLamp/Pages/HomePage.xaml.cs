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

                var confirmPassword = await DisplayPromptAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_ConfirmNewPassword, AppResources.Global_Ok, AppResources.Global_Cancel, AppResources.Global_Password, maxLength: 128);
                if (confirmPassword == null) return;

                currentPassword = currentPassword.Trim();
                newPassword = newPassword.Trim();
                confirmPassword = confirmPassword.Trim();

                if (newPassword.Length < 6)
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_NewPasswordTooShort, AppResources.Global_Ok);
                    return;
                }

                if (newPassword != confirmPassword)
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_PasswordsDoNotMatch, AppResources.Global_Ok);
                    return;
                }

                try
                {
                    await _backend.ChangePasswordAsync(_state.Token!, currentPassword, newPassword);
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_PasswordUpdated, AppResources.Global_Ok);
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
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_CurrentPasswordIncorrect, AppResources.Global_Ok);
                }
                catch (BackendHttpException ex) when (ex.Code == "new_password_too_short")
                {
                    await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_ChangePassword, AppResources.Home_AccountHamburgerMenu_NewPasswordTooShort, AppResources.Global_Ok);
                }
                catch (Exception ex)
                {
                    await DisplayAlertAsync(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
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
                var confirm = await DisplayAlertAsync(AppResources.Home_AccountHamburgerMenu_DeleteAccount, AppResources.Home_AccountHamburgerMenu_DeleteAccount_ConfirmMessage, AppResources.Home_AccountHamburgerMenu_DeleteAccount_ConfirmDeleteButton, AppResources.Global_Cancel);
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
                    await DisplayAlertAsync(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
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
        var username = await DisplayPromptAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_EnterUsername, AppResources.Global_Ok, AppResources.Global_Cancel);
        if (string.IsNullOrWhiteSpace(username)) return;

        try
        {
            await _backend.ForgotPasswordAsync(username.Trim());
            await DisplayAlertAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_Sent, AppResources.Global_Ok);
        }
        catch (BackendHttpException ex) when (ex.Code == "email_not_set")
        {
            await DisplayAlertAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_NoEmail, AppResources.Global_Ok);
        }
        catch (BackendHttpException ex) when (ex.Code == "user_not_found")
        {
            await DisplayAlertAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_UserNotFound, AppResources.Global_Ok);
        }
        catch (BackendHttpException ex) when (ex.Code == "email_send_failed")
        {
            await DisplayAlertAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_EmailSendFailed, AppResources.Global_Ok);
        }
        catch
        {
            await DisplayAlertAsync(AppResources.Home_PasswordRecovery_Title, AppResources.Home_PasswordRecovery_ConnectionFailure, AppResources.Global_Ok);
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
