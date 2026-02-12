namespace LinkedLamp.Services;

public class AppState
{
    public string? UserName { get; set; }
    public string? UserId { get; set; }
    public string? UserToken { get; set; }
    public List<GroupDto> GroupsCache { get; set; } = new();
    public string? SelectedGroupId { get; set; }
}
