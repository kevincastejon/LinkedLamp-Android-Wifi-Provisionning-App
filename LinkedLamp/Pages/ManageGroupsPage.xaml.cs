using LinkedLamp.Services;
using LinkedLamp.Resources.Strings;

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
        await RefreshGroupsAsync();
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
            await RefreshGroupsAsync();
        }
        finally
        {
            _refreshInProgress = false;
            GroupsRefreshView.IsRefreshing = false;
        }
    }

    private async Task RefreshGroupsAsync()
    {
        StatusLabel.Text = AppResources.ManageGroup_Loading;

        MyGroupsView.ItemsSource = null;
        FriendsGroupsView.ItemsSource = null;
        MyGroupsLabel.IsVisible = false;
        FriendsGroupsLabel.IsVisible = false;
        if (string.IsNullOrWhiteSpace(_state.UserToken))
        {
            await Navigation.PopToRootAsync();
            return;
        }

        try
        {
            var groups = await _backend.GetGroupsAsync(_state.UserToken);
            _state.GroupsCache = groups;
            List<GroupDto> myGroupsList = groups.Where(x => x.IsOwner).ToList();
            List<GroupDto> friendsGroupsList = groups.Where(x => !x.IsOwner).ToList();
            MyGroupsView.ItemsSource = myGroupsList;
            FriendsGroupsView.ItemsSource = friendsGroupsList;
            MyGroupsLabel.IsVisible = myGroupsList.Count > 0;
            FriendsGroupsLabel.IsVisible = friendsGroupsList.Count > 0;
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
            await DisplayAlertAsync(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
            await Navigation.PopAsync();
        }
    }

    private async void OnCreateGroupClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_state.UserToken)) return;

        var name = await DisplayPromptAsync(AppResources.ManageGroups_CreateGroupTitle, AppResources.ManageGroups_GroupName, AppResources.ManageGroups_Create, AppResources.Global_Cancel);
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            await _backend.CreateGroupAsync(_state.UserToken, name.Trim());
            await RefreshGroupsAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlertAsync(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }

    private async void OnMyGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as GroupDto;
        if (selected == null) return;

        MyGroupsView.SelectedItem = null;

        if (string.IsNullOrWhiteSpace(selected.Id)) return;

        _manageGroupPage.SetGroupId(selected.Id);
        await Navigation.PushAsync(_manageGroupPage);
    }
    private async void OnFriendsGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as GroupDto;
        if (selected == null) return;

        FriendsGroupsView.SelectedItem = null;

        if (string.IsNullOrWhiteSpace(selected.Id)) return;

        _manageGroupPage.SetGroupId(selected.Id);
        await Navigation.PushAsync(_manageGroupPage);
    }

}
