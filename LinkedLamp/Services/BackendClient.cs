using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LinkedLamp.Services;

public class BackendClient
{
    private readonly HttpClient _http;
    private readonly AppState _state;

    public BackendClient(HttpClient http, AppState state)
    {
        _http = http;
        _state = state;
    }

    public string BaseUrl
    {
        get => Preferences.Get("BackendBaseUrl", "http://192.168.1.7:3000").TrimEnd('/');
        set => Preferences.Set("BackendBaseUrl", value.Trim().TrimEnd('/'));
    }

    public async Task<string?> LoadTokenAsync()
    {
        try
        {
            var t = await SecureStorage.GetAsync("UserToken");
            _state.UserToken = string.IsNullOrWhiteSpace(t) ? null : t;
            return _state.UserToken;
        }
        catch
        {
            _state.UserToken = null;
            return null;
        }
    }

    public async Task SaveUserIdentityAsync(string userToken, string userId, string userName)
    {
        _state.UserToken = userToken;
        _state.UserId = userId;
        _state.UserName = userName;
        await SecureStorage.SetAsync("UserToken", userToken);
    }

    public void ClearToken()
    {
        _state.UserToken = null;
        SecureStorage.Remove("UserToken");
    }
    public async Task ForgotPasswordAsync(string username)
    {
        await SendAsync<string>(HttpMethod.Post, "/forgot-password", null, new { username });
    }
    public async Task ChangePasswordAsync(string token, string currentPassword, string newPassword)
    {
        await SendAsync<string>(HttpMethod.Post, "/me/change-password", token, new { currentPassword, newPassword });
    }


    private HttpRequestMessage NewRequest(HttpMethod method, string path, string? token, object? body)
    {
        var uri = new Uri($"{BaseUrl}{path}");
        var req = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body != null)
        {
            var json = JsonSerializer.Serialize(body);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return req;
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage resp)
    {
        return await resp.Content.ReadAsStringAsync();
    }

    private static string? TryGetErrorCode(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                if (e.ValueKind == JsonValueKind.String) return e.GetString();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static BackendHttpException HttpError(HttpStatusCode code, string body)
    {
        return new BackendHttpException((int)code, TryGetErrorCode(body), body);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, string? token, object? body)
    {
        using var req = NewRequest(method, path, token, body);
        using var resp = await _http.SendAsync(req);

        var raw = await ReadBodyAsync(resp);

        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            var err = TryGetErrorCode(raw);
            if (err == "invalid_token" || err == "missing_token") throw new BackendAuthException(err);
        }

        if (!resp.IsSuccessStatusCode) throw HttpError(resp.StatusCode, raw);

        if (typeof(T) == typeof(string)) return (T)(object)raw;

        try
        {
            var obj = JsonSerializer.Deserialize<T>(raw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (obj == null) throw new BackendHttpException((int)resp.StatusCode, "invalid_json", raw);
            return obj;
        }
        catch
        {
            throw new BackendHttpException((int)resp.StatusCode, "invalid_json", raw);
        }
    }

    public async Task<(string, string, string)> RegisterAsync(string username, string password, string? email)
    {
        var res = await SendAsync<AuthResponse>(HttpMethod.Post, "/register", null, new { username, password, email });
        if (string.IsNullOrWhiteSpace(res.Token)) throw new BackendHttpException(0, "missing_token", "");
        if (string.IsNullOrWhiteSpace(res.UserId)) throw new BackendHttpException(0, "missing_userId", "");
        if (string.IsNullOrWhiteSpace(res.UserName)) throw new BackendHttpException(0, "missing_userName", "");
        return new(res.Token, res.UserId, res.UserName);
    }

    public async Task<(string, string, string)> LoginAsync(string username, string password)
    {
        var res = await SendAsync<AuthResponse>(HttpMethod.Post, "/login", null, new { username, password });
        if (string.IsNullOrWhiteSpace(res.Token)) throw new BackendHttpException(0, "missing_token", "");
        if (string.IsNullOrWhiteSpace(res.UserId)) throw new BackendHttpException(0, "missing_userid", "");
        if (string.IsNullOrWhiteSpace(res.UserName)) throw new BackendHttpException(0, "missing_username", "");
        return new(res.Token, res.UserId, res.UserName);
    }
    public async Task<(string, string)> GetUserInfoAsync(string token)
    {
        var res = await SendAsync<AuthResponse>(HttpMethod.Get, "/me", token, null);
        if (string.IsNullOrWhiteSpace(res.UserId)) throw new BackendHttpException(0, "missing_userid", "");
        if (string.IsNullOrWhiteSpace(res.UserName)) throw new BackendHttpException(0, "missing_username", "");
        return new(res.UserId, res.UserName);
    }
    public async Task<List<GroupDto>> GetGroupsAsync(string token)
    {
        var res = await SendAsync<GroupsResponse>(HttpMethod.Get, "/groups", token, null);
        return res.Groups ?? new List<GroupDto>();
    }

    public async Task<GroupDto> CreateGroupAsync(string token, string name)
    {
        var res = await SendAsync<GroupResponse>(HttpMethod.Post, "/groups", token, new { name });
        if (res.Group == null) throw new BackendHttpException(0, "missing_group", "");
        return res.Group;
    }

    public async Task<GroupDto> RenameGroupAsync(string token, string groupId, string name)
    {
        var res = await SendAsync<GroupResponse>(HttpMethod.Patch, $"/groups/{groupId}", token, new { name });
        if (res.Group == null) throw new BackendHttpException(0, "missing_group", "");
        return res.Group;
    }

    public async Task DeleteGroupAsync(string token, string groupId)
    {
        await SendAsync<string>(HttpMethod.Delete, $"/groups/{groupId}", token, null);
    }

    public async Task LeaveGroupAsync(string token, string groupId)
    {
        await SendAsync<string>(HttpMethod.Post, $"/groups/{groupId}/leave", token, null);
    }

    public async Task AddMemberAsync(string token, string groupId, string username)
    {
        await SendAsync<string>(HttpMethod.Post, $"/groups/{groupId}/members", token, new { username });
    }

    public async Task<List<MemberDto>> ListMembersAsync(string token, string groupId)
    {
        var res = await SendAsync<MembersResponse>(HttpMethod.Get, $"/groups/{groupId}/members", token, null);
        return res.Members ?? new List<MemberDto>();
    }

    public async Task RemoveMemberAsync(string token, string groupId, string userId)
    {
        await SendAsync<string>(HttpMethod.Delete, $"/groups/{groupId}/members/{userId}", token, null);
    }

    public async Task DeleteAccountAsync(string token)
    {
        await SendAsync<string>(HttpMethod.Delete, "/me", token, null);
    }
}

public class BackendAuthException : Exception
{
    public string? Code { get; }
    public BackendAuthException(string? code) : base(code ?? "auth_error") { Code = code; }
}

public class BackendHttpException : Exception
{
    public int Status { get; }
    public string? Code { get; }
    public string Body { get; }
    public BackendHttpException(int status, string? code, string body) : base(code ?? $"http_{status}")
    {
        Status = status;
        Code = code;
        Body = body ?? "";
    }
}

public class AuthResponse
{
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
}

public class GroupsResponse
{
    public List<GroupDto>? Groups { get; set; }
}

public class GroupResponse
{
    public GroupDto? Group { get; set; }
}

public class MembersResponse
{
    public List<MemberDto>? Members { get; set; }
}

public class GroupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? OwnerUserId { get; set; }
    public bool IsDefault { get; set; }
    public string? Role { get; set; }
    public bool CanLeave { get; set; }
    public bool CanRename { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManageMembers { get; set; }
    public bool IsOwner => Role == "owner";

    public override string ToString()
    {
        return Name ?? "";
    }
}

public class MemberDto
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
}
