using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSharpCodeAnalyst.Features.Ai;

/// <summary>
///     Sends a single user prompt to an LLM endpoint and returns the response text.
///     Supports Anthropic Messages API and OpenAI-compatible chat completions API.
///     The format is auto-detected based on the endpoint URL.
/// </summary>
public class AiClient
{
    private static readonly HttpClient HttpClient = new();

    public async Task<string> SendAsync(
        string endpoint,
        string apiKey,
        string model,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (endpoint.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase))
        {
            return await SendAnthropicAsync(endpoint, apiKey, model, prompt, cancellationToken);
        }

        return await SendOpenAiAsync(endpoint, apiKey, model, prompt, cancellationToken);
    }

    private static async Task<string> SendAnthropicAsync(
        string endpoint,
        string apiKey,
        string model,
        string prompt,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model,
            max_tokens = 4096,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var response = await HttpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiClientException($"API error {(int)response.StatusCode}: {json}");
        }

        var node = JsonNode.Parse(json);
        var text = node?["content"]?[0]?["text"]?.GetValue<string>();
        return text ?? throw new AiClientException("Unexpected Anthropic response format.");
    }

    private static async Task<string> SendOpenAiAsync(
        string endpoint,
        string apiKey,
        string model,
        string prompt,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        var response = await HttpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AiClientException($"API error {(int)response.StatusCode}: {json}");
        }

        var node = JsonNode.Parse(json);
        var text = node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        return text ?? throw new AiClientException("Unexpected OpenAI response format.");
    }
}
