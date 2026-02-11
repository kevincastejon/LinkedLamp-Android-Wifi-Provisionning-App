using LinkedLamp.Services;
using LinkedLamp.Resources.Strings;

namespace LinkedLamp.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;
    private bool _passwordVisible;
    public RegisterPage(AppState state, BackendClient backend)
    {
        InitializeComponent();
        _state = state;
        _backend = backend;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "";
        var username = (UsernameEntry.Text ?? "").Trim();
        var password = PasswordEntry.Text ?? "";
        var email = (EmailEntry.Text ?? "").Trim();
        if (email.Length == 0) email = null;

        if (username.Length == 0 || password.Length == 0)
        {
            StatusLabel.Text = AppResources.Register_UsernamePasswordRequired;
            return;
        }

        try
        {
            var token = await _backend.RegisterAsync(username, password, email);
            await _backend.SaveTokenAsync(token);
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