using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class GroqAiService
{
    private readonly HttpClient _httpClient = new();
    private readonly string _apiKey;

    public GroqAiService(IConfiguration config)
    {
        _apiKey = config["Groq:ApiKey"] ?? "";
    }

    public async Task<string?> GenerateDfdAsync(string abstractText)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var prompt = $@"
Analyze the following project abstract and return DFD guidance strictly in JSON with:
- dfd_level
- external_entities
- processes
- data_stores
- data_flows

Abstract:
{abstractText}
";

        var requestBody = new
        {
            model = "llama3-8b-8192",
            messages = new[]
            {
                new { role = "system", content = "You are a software design expert." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync(
            "https://api.groq.com/openai/v1/chat/completions",
            new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            )
        );

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadAsStringAsync();
    }
}
