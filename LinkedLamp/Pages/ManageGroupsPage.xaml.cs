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

        GroupsView.ItemsSource = null;

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

            var rows = new List<GroupsRow>();

            if (myGroupsList.Count > 0)
            {
                rows.Add(new GroupsRow { IsHeader = true, Title = AppResources.ManageGroups_MyGroupsSubtitle });
                rows.AddRange(myGroupsList.Select(g => new GroupsRow
                {
                    IsGroup = true,
                    Id = g.Id ?? "",
                    Name = g.Name ?? "",
                    IsOwner = g.IsOwner
                }));
            }

            if (friendsGroupsList.Count > 0)
            {
                rows.Add(new GroupsRow { IsHeader = true, Title = AppResources.ManageGroups_FriendsGroupsSubtitle });
                rows.AddRange(friendsGroupsList.Select(g => new GroupsRow
                {
                    IsGroup = true,
                    Id = g.Id ?? "",
                    Name = g.Name ?? "",
                    IsOwner = g.IsOwner
                }));
            }

            GroupsView.ItemsSource = rows;
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

    private async void OnGroupSelected(object sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection.FirstOrDefault() as GroupsRow;
        if (selected == null) return;

        GroupsView.SelectedItem = null;

        if (!selected.IsGroup) return;
        if (string.IsNullOrWhiteSpace(selected.Id)) return;

        _manageGroupPage.SetGroupId(selected.Id);
        await Navigation.PushAsync(_manageGroupPage);
    }

    public class GroupsRow
    {
        public bool IsHeader { get; set; }
        public bool IsGroup { get; set; }

        public string Title { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsOwner { get; set; }
    }
}
