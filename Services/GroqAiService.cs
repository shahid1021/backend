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
Based on the following project abstract, provide DETAILED guidance on how to CREATE a DFD (Data Flow Diagram).

Include:
1. NUMBER OF MODULES IDENTIFIED in the project
2. DETAILED SHAPE GUIDE:
   - What are the external entities? (Draw as circles)
   - What processes are needed? (Draw as circles)
   - What data stores? (Draw as parallel lines/rectangles)
3. ARROW PLACEMENT GUIDE:
   - Show exact data flows between entities and processes
   - Show data flow from processes to data stores
   - Label each arrow with data type flowing through
4. STEP-BY-STEP INSTRUCTIONS:
   - In what order to draw shapes
   - Where to place them on the diagram
   - How to connect them with arrows

Project Abstract:
{abstractText}

Format the response with clear sections and bullet points.
";

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = new[]
            {
                new { role = "system", content = "You are a software architecture expert specializing in DFD diagrams. Provide very detailed, step-by-step guidance." },
                new { role = "user", content = prompt }
            },
            temperature = 0.5,
            max_tokens = 2048
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
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

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            var content = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GenerateDfdAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> ChatAsync(string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Console.WriteLine("❌ ERROR: Groq API key is missing!");
            return null;
        }

        Console.WriteLine($"📨 Sending message to Groq: {userMessage}");

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = new[]
            {
                new { role = "system", content = "You are a helpful AI assistant." },
                new { role = "user", content = userMessage }
            },
            temperature = 0.7,
            max_tokens = 1024
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            var response = await _httpClient.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            Console.WriteLine($"📊 Groq Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Groq API Error: {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"✅ Groq Response: {responseContent}");

            var jsonDoc = JsonDocument.Parse(responseContent);
            var content = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            Console.WriteLine($"✨ AI Response: {content}");
            return content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Exception in ChatAsync: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            return null;
        }
    }

    public async Task<string?> CheckSimilarityAsync(string uploadedAbstract, string existingAbstract, string existingTitle)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return null;

        var prompt = $@"Compare these two project abstracts and give a similarity percentage (0-100).

UPLOADED ABSTRACT:
{uploadedAbstract}

EXISTING PROJECT: {existingTitle}
EXISTING ABSTRACT:
{existingAbstract}

Respond ONLY in this exact JSON format, nothing else:
{{""similarity"": <number>, ""reason"": ""<brief explanation>""}}";

        var requestBody = new
        {
            model = "llama-3.1-8b-instant",
            messages = new[]
            {
                new { role = "system", content = "You are a plagiarism detection expert. Compare abstracts and return similarity percentage as JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 256
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            var response = await _httpClient.PostAsync(
                "https://api.groq.com/openai/v1/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            if (!response.IsSuccessStatusCode) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in CheckSimilarityAsync: {ex.Message}");
            return null;
        }
    }
}
