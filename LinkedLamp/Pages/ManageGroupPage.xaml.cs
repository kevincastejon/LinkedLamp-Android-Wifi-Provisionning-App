using LinkedLamp.Services;
using LinkedLamp.Resources.Strings;

namespace LinkedLamp.Pages;

public partial class ManageGroupPage : ContentPage
{
    private readonly AppState _state;
    private readonly BackendClient _backend;

    private string? _groupId;
    private GroupDto? _group;
    private List<MemberDto> _members = new();
    private bool _refreshInProgress;

    public ManageGroupPage(AppState state, BackendClient backend)
    {
        InitializeComponent();
        _state = state;
        _backend = backend;
    }

    public void SetGroupId(string groupId)
    {
        _groupId = groupId;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync(bool clearList = true, bool showLoadingLabel = true)
    {
        if (showLoadingLabel)
            StatusLabel.Text = AppResources.ManageGroup_Loading;
        else
            StatusLabel.Text = "";

        if (clearList)
            MembersView.ItemsSource = null;

        if (string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId))
        {
            await Navigation.PopToRootAsync();
            return;
        }

        _group = _state.GroupsCache.FirstOrDefault(g => g.Id == _groupId);

        if (_group == null)
        {
            try
            {
                var groups = await _backend.GetGroupsAsync(_state.Token);
                _state.GroupsCache = groups;
                _group = _state.GroupsCache.FirstOrDefault(g => g.Id == _groupId);
            }
            catch (BackendAuthException)
            {
                _backend.ClearToken();
                _state.GroupsCache.Clear();
                await Navigation.PopToRootAsync();
                return;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "";
                await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
                await Navigation.PopAsync();
                return;
            }
        }

        if (_group == null)
        {
            StatusLabel.Text = "";
            await Navigation.PopAsync();
            return;
        }

        Title = _group.Name ?? AppResources.ManageGroup_PageTitle;
        HeaderLabel.Text = _group.Name ?? "";

        RenameButton.IsVisible = _group.CanRename;
        DeleteButton.IsVisible = _group.CanDelete;
        LeaveButton.IsVisible = _group.CanLeave;
        AddUserButton.IsVisible = _group.CanManageMembers;
        Log($"[LoadAsync] CanManageMembers {_group.CanManageMembers}");

        try
        {
            _members = await _backend.ListMembersAsync(_state.Token, _groupId);
            Log($"[LoadAsync] Members count {_members.Count}");
            MembersView.ItemsSource = _members.Select(m => new MemberRow(m, CanRemoveMember(m))).ToList();
        }
        catch (BackendHttpException ex)
        {
            Log($"[LoadAsync] <Exception> BackendHttpException {ex.Message}");
            StatusLabel.Text = ex.Code ?? ex.Message;
            return;
        }
        catch (BackendAuthException ex)
        {
            Log($"[LoadAsync] <Exception> BackendAuthException {ex.Message}");
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
            return;
        }
        catch (Exception ex)
        {
            Log($"[LoadAsync] <Exception> {ex.Message}");
            StatusLabel.Text = ex.Message;
            return;
        }

        StatusLabel.Text = "";
    }


    private bool CanRemoveMember(MemberDto m)
    {
        if (_group == null) return false;
        if (!_group.CanManageMembers) return false;
        if (string.Equals(m.Role, "owner", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private async void OnRenameClicked(object sender, EventArgs e)
    {
        if (_group == null || string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId)) return;

        var name = await DisplayPromptAsync(AppResources.ManageGroup_RenameGroup_Title, AppResources.ManageGroup_RenameGroup_NewName, AppResources.ManageGroup_RenameGroup_Rename, AppResources.Global_Cancel, initialValue: _group.Name ?? "");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            var updated = await _backend.RenameGroupAsync(_state.Token, _groupId, name.Trim());
            var idx = _state.GroupsCache.FindIndex(x => x.Id == _groupId);
            if (idx >= 0) _state.GroupsCache[idx] = updated;
            await LoadAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        if (_group == null || string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId)) return;

        var confirm = await DisplayAlert(AppResources.ManageGroup_LeaveGroup_Title, AppResources.ManageGroup_LeaveGroup_ConfirmMessage, AppResources.ManageGroup_LeaveGroup_Leave, AppResources.Global_Cancel);
        if (!confirm) return;

        try
        {
            await _backend.LeaveGroupAsync(_state.Token, _groupId);
            _state.GroupsCache = await _backend.GetGroupsAsync(_state.Token);
            await Navigation.PopAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }

    private async void OnAddUserClicked(object sender, EventArgs e)
    {
        if (_group == null || string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId)) return;

        var username = await DisplayPromptAsync(AppResources.ManageGroup_AddUser_Title, AppResources.ManageGroup_AddUser_Username, AppResources.ManageGroup_AddUser_Add, AppResources.Global_Cancel);
        if (string.IsNullOrWhiteSpace(username)) return;

        try
        {
            await _backend.AddMemberAsync(_state.Token, _groupId, username.Trim());
            await LoadAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_group == null || string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId)) return;

        var confirm = await DisplayAlert(AppResources.ManageGroup_DeleteGroup_Title, AppResources.ManageGroup_DeleteGroup_ConfirmMessage, AppResources.ManageGroup_DeleteGroup_Delete, AppResources.Global_Cancel);
        if (!confirm) return;

        try
        {
            await _backend.DeleteGroupAsync(_state.Token, _groupId);
            _state.GroupsCache = await _backend.GetGroupsAsync(_state.Token);
            await Navigation.PopAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }

    private async void OnRemoveMemberClicked(object sender, EventArgs e)
    {
        if (_group == null || string.IsNullOrWhiteSpace(_state.Token) || string.IsNullOrWhiteSpace(_groupId)) return;
        if (sender is not Button btn) return;
        if (btn.BindingContext is not MemberRow row) return;
        if (!row.CanRemove) return;

        var confirm = await DisplayAlert(AppResources.ManageGroup_RemoveUser_Title, string.Format(AppResources.ManageGroup_RemoveUser_ConfirmMessageFormat, row.Username), AppResources.ManageGroup_RemoveUser_Remove, AppResources.Global_Cancel);
        if (!confirm) return;

        try
        {
            await _backend.RemoveMemberAsync(_state.Token, _groupId, row.UserId);
            await LoadAsync();
        }
        catch (BackendAuthException)
        {
            _backend.ClearToken();
            _state.GroupsCache.Clear();
            await Navigation.PopToRootAsync();
        }
        catch (BackendHttpException ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Code ?? ex.Message, AppResources.Global_Ok);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Global_Error, ex.Message, AppResources.Global_Ok);
        }
    }
    private async void OnMembersRefreshing(object sender, EventArgs e)
    {
        if (_refreshInProgress)
        {
            MembersRefreshView.IsRefreshing = false;
            return;
        }

        _refreshInProgress = true;

        try
        {
            await LoadAsync(clearList: false, showLoadingLabel: false);
        }
        finally
        {
            _refreshInProgress = false;
            MembersRefreshView.IsRefreshing = false;
        }
    }


    private record MemberRow(string UserId, string Username, string Role, bool CanRemove)
    {
        public MemberRow(MemberDto m, bool canRemove) : this(m.UserId ?? "", m.Username ?? "", m.Role ?? "", canRemove) { }
    }
    private void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[LinkedLamp] [ManageGroupPage] {message}");
    }
}
