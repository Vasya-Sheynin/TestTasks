using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace YoutubeSummarize.Application.Interfaces;

public class MistralAiService : ISummarizationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey; // Set this via configuration

    public MistralAiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["MistralAi:ApiKey"];
    }

    public async Task<string> SummarizeAsync(string text)
    {
        var requestBody = new
        {
            model = "mistral-small-latest",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant that summarizes text." },
                new { role = "user", content = $"Summarize the following text in 3 to 5 sentences:\n{text}" }
            },
            max_tokens = 512,
            temperature = 0.7,
            response_format = new { type = "text" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.mistral.ai/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new Exception("No summary returned from Mistral AI.");
        var summary = choices[0].GetProperty("message").GetProperty("content").GetString();
        return summary;
    }
}
