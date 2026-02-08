using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using System.Text.Json;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    [HttpPost("dfd-guidance")]
    public async Task<IActionResult> GetDfdGuidance(
        [FromBody] DfdRequest request,
        [FromServices] GroqAiService groqAi,
        [FromServices] FileTextExtractor extractor
    )
    {
        // 🔴 request.FilePath must come from frontend or DB
        if (string.IsNullOrWhiteSpace(request.FilePath))
            return BadRequest("File path is required");

        if (!System.IO.File.Exists(request.FilePath))
            return BadRequest("File not found on server");

        // 1️⃣ Extract text from uploaded file
        var extractedText = extractor.ExtractText(request.FilePath);

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest("Could not extract text from file");

        // 2️⃣ Send extracted text to GROQ AI
        var groqResponse = await groqAi.GenerateDfdAsync(extractedText);

        if (!string.IsNullOrEmpty(groqResponse))
        {
            Console.WriteLine("🚀 GROQ AI USED (FILE-BASED)");
            return Ok(JsonDocument.Parse(groqResponse).RootElement);
        }

        // 3️⃣ Fallback (safety)
        Console.WriteLine("⚠️ FALLBACK USED");
        return Ok(new
        {
            dfd_level = "Level-0",
            external_entities = new[] { "Student", "Faculty" },
            processes = new[] { "Upload Project", "Review Project" },
            data_stores = new[] { "Project Database" },
            data_flows = new[]
            {
                "Student → Upload Project",
                "Upload Project → Project Database"
            }
        });
    }
}
