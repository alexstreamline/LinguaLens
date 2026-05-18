using System.Net.Http.Headers;
using LinguaLens.Core.Interfaces;

namespace LinguaLens.Infrastructure.Llm;

/// <summary>
/// DelegatingHandler that injects the Bearer API key from IAppSettings on every outgoing request.
/// Reads the key at request time so key changes take effect immediately.
/// </summary>
public class ApiKeyHandler : DelegatingHandler
{
    private readonly IAppSettings _settings;

    public ApiKeyHandler(IAppSettings settings) => _settings = settings;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        return base.SendAsync(request, ct);
    }
}
