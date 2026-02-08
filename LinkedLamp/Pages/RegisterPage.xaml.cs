using LinkedLamp.Services;

namespace LinkedLamp.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;

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
            StatusLabel.Text = "Username and password are required.";
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
}
