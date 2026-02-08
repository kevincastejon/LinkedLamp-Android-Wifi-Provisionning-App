namespace LinkedLamp.Services;

public class AppState
{
    public string? Token { get; set; }
    public List<GroupDto> GroupsCache { get; set; } = new();
    public string? SelectedGroupId { get; set; }
}
