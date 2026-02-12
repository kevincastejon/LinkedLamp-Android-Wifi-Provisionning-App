using LinkedLamp.Resources.Strings;
using LinkedLamp.Services;

namespace LinkedLamp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;
    private bool _passwordVisible;
    public LoginPage(AppState state, BackendClient backend)
    {
        InitializeComponent();
        _state = state;
        _backend = backend;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        var username = (UsernameEntry.Text ?? "").Trim();
        var password = PasswordEntry.Text ?? "";

        if (username.Length == 0 || password.Length == 0)
        {
            StatusLabel.Text = AppResources.Login_RequiredFieldsError;
            return;
        }

        try
        {
            var userIdentity = await _backend.LoginAsync(username, password);
            await _backend.SaveUserIdentityAsync(userIdentity.Item1, userIdentity.Item2, userIdentity.Item3);
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            StatusLabel.Text = ex.Code ?? ex.Message;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }
    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        _passwordVisible = !_passwordVisible;

        PasswordEntry.IsPassword = !_passwordVisible;

        TogglePasswordButton.Source = _passwordVisible
            ? "eye_open.png"
            : "eye_closed.png";
    }
}
