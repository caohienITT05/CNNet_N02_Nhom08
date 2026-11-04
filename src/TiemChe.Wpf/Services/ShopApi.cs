using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TiemChe.Contracts.Catalog;

namespace TiemChe.Wpf.Services;

public sealed class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed record ApiConfiguration(Uri BaseAddress)
{
    public static ApiConfiguration FromEnvironment()
    {
        var value = Environment.GetEnvironmentVariable("TIEMCHE_API_URL") ?? "https://localhost:7048/";
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var address)
            || address.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(address.UserInfo)
            || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            throw new InvalidOperationException("TIEMCHE_API_URL phải là địa chỉ HTTPS hợp lệ.");
        return new ApiConfiguration(address);
    }
}

public sealed class ShopApi(HttpClient http, Session session)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<T> GetAsync<T>(string route, CancellationToken ct) => ReadAsync<T>(HttpMethod.Get, route, null, ct);
    public Task<T> PostAsync<T>(string route, object body, CancellationToken ct) => ReadAsync<T>(HttpMethod.Post, route, body, ct);
    public Task<T> PutAsync<T>(string route, object body, CancellationToken ct) => ReadAsync<T>(HttpMethod.Put, route, body, ct);
    public Task PostAsync(string route, object body, CancellationToken ct) => DiscardAsync(HttpMethod.Post, route, body, ct);
    public Task PutAsync(string route, object body, CancellationToken ct) => DiscardAsync(HttpMethod.Put, route, body, ct);
    public Task DeleteAsync(string route, CancellationToken ct) => DiscardAsync(HttpMethod.Delete, route, null, ct);

    public async Task<T> DeleteAsync<T>(string route, CancellationToken ct) =>
        await ReadAsync<T>(HttpMethod.Delete, route, null, ct);

    public async Task<byte[]> DownloadAsync(string route, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Get, route, null, ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<string> UploadImageAsync(string file, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        if (stream.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Ảnh tối đa 5 MB.");
        using var content = new StreamContent(stream);
        content.Headers.ContentLength = stream.Length;
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await SendAsync(HttpMethod.Post, "admin/images", content, ct);
        return (await ReadBodyAsync<UploadedImage>(response, ct)).Path;
    }

    public static string Query(string route, params (string Key, object? Value)[] parameters) =>
        route + "?" + string.Join("&", parameters.Where(p => p.Value is not null).Select(p =>
            $"{p.Key}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "")}"));

    private async Task<T> ReadAsync<T>(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        using var response = await SendAsync(method, route, Content(body), ct);
        return await ReadBodyAsync<T>(response, ct);
    }

    private async Task DiscardAsync(HttpMethod method, string route, object? body, CancellationToken ct)
    {
        using var response = await SendAsync(method, route, Content(body), ct);
    }

    private static HttpContent? Content(object? body) => body is null ? null : JsonContent.Create(body, options: Json);

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new JsonException("Phản hồi API trống.");

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, HttpContent? content,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/" + route) { Content = content };
        var token = session.AccessToken;
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.IsSuccessStatusCode)
            return response;
        using (response)
        {
            var detail = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Vui lòng đăng nhập để tiếp tục.",
                HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
                _ => "Không thể thực hiện thao tác. Vui lòng thử lại."
            };
            try
            {
                using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Json, ct);
                if (problem is not null && problem.RootElement.TryGetProperty("detail", out var value)
                    && value.ValueKind == JsonValueKind.String)
                    detail = value.GetString() ?? detail;
            }
            catch (JsonException) { }
            if (response.StatusCode == HttpStatusCode.Unauthorized && token is not null && session.AccessToken == token)
                session.Clear(expired: true);
            throw new ApiException(response.StatusCode, detail);
        }
    }
}
