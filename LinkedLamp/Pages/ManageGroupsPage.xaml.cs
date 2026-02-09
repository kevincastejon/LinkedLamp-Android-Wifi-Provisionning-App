using LinkedLamp.Services;

namespace LinkedLamp.Pages;

public partial class ManageGroupsPage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;
    private readonly ManageGroupPage _manageGroupPage;

    private bool _refreshInProgress;

    public ManageGroupsPage(AppState state, BackendClient backend, ManageGroupPage manageGroupPage)
    {
        InitializeComponent();
        _state = state;
        _backend = backend;
        _manageGroupPage = manageGroupPage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshGroupsAsync(clearList: true);
    }

    private async void OnGroupsRefreshing(object sender, EventArgs e)
    {
        if (_refreshInProgress)
        {
            GroupsRefreshView.IsRefreshing = false;
            return;
        }

        _refreshInProgress = true;

        try
        {
            await RefreshGroupsAsync(clearList: false);
        }
        finally
        {
            _refreshInProgress = false;
            GroupsRefreshView.IsRefreshing = false;
        }
    }

    private async Task RefreshGroupsAsync(bool clearList)
    {
        StatusLabel.Text = "Loading...";

        if (clearList)
            GroupsView.ItemsSource = null;

        if (string.IsNullOrWhiteSpace(_state.Token))
        {
            await Navigation.PopToRootAsync();
            return;
        }

        try
        {
            var groups = await _backend.GetGroupsAsync(_state.Token);
            _state.GroupsCache = groups;
            GroupsView.ItemsSource = _state.GroupsCache;
            StatusLabel.Text = "";
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "";
            await DisplayAlert("Error", ex.Message, "OK");
            await Navigation.PopAsync();
        }
    }

    private async void OnCreateGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.Token)) return;

        var name = await DisplayPromptAsync("Create group", "Group name", "Create", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            await _backend.CreateGroupAsync(_state.Token, name.Trim());
            await RefreshGroupsAsync(clearList: true);
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert("Error", ex.Code ?? ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as GroupDto;
        if (selected == null) return;

        GroupsView.SelectedItem = null;

        if (string.IsNullOrWhiteSpace(selected.Id)) return;

        _manageGroupPage.SetGroupId(selected.Id);
        await Navigation.PushAsync(_manageGroupPage);
    }
}
