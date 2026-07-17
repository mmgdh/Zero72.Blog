using System.Net;
using System.Net.Http.Json;
using Zero72.Blog.Shared;

namespace Zero72.Blog.Admin.Services;

public sealed class AdminAuthClient(HttpClient http)
{
    public async Task<AdminAuthStatus> GetStatusAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<AdminAuthStatus>("api/admin/auth/me") ??
                new AdminAuthStatus(false, null);
        }
        catch (HttpRequestException)
        {
            return new AdminAuthStatus(false, null);
        }
    }

    public async Task<AdminAuthStatus?> LoginAsync(string userName, string password)
    {
        var response = await http.PostAsJsonAsync("api/admin/auth/login", new AdminLoginRequest(userName, password));
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.TooManyRequests)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdminAuthStatus>();
    }

    public async Task LogoutAsync()
    {
        var response = await http.PostAsync("api/admin/auth/logout", null);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
