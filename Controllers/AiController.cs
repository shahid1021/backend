using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using System.Text.Json;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    [HttpPost("dfd-guidance")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetDfdGuidance(
        [FromForm] IFormFile file,
        [FromServices] GroqAiService groqAi,
        [FromServices] FileTextExtractor extractor
    )
    {
        Console.WriteLine("📋 DFD GUIDANCE ENDPOINT CALLED!");

        if (file == null || file.Length == 0)
        {
            Console.WriteLine("❌ No file uploaded");
            return BadRequest(new { error = "No file uploaded" });
        }

        try
        {
            // 1️⃣ Save file temporarily and extract text
            Console.WriteLine($"📄 Processing file: {file.FileName}");
            var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);

            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            Console.WriteLine($"✅ File saved to: {tempPath}");

            // 2️⃣ Extract text
            var extractedText = extractor.ExtractText(tempPath);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                Console.WriteLine("❌ Could not extract text from file");
                System.IO.File.Delete(tempPath);
                return BadRequest(new { error = "Could not extract text from file" });
            }

            Console.WriteLine($"✅ Extracted text length: {extractedText.Length} characters");

            // 3️⃣ Send extracted text to GROQ AI for detailed DFD guidance
            Console.WriteLine("🤖 Sending to Groq for DFD guidance...");
            var dfdGuidance = await groqAi.GenerateDfdAsync(extractedText);

            // 4️⃣ Clean up
            System.IO.File.Delete(tempPath);

            if (!string.IsNullOrEmpty(dfdGuidance))
            {
                Console.WriteLine("✨ DFD Guidance generated successfully");
                return Ok(new { guidance = dfdGuidance });
            }

            Console.WriteLine("⚠️ Failed to get DFD guidance from Groq");
            return BadRequest(new { error = "Failed to generate DFD guidance" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Exception: {ex.Message}");
            return BadRequest(new { error = $"Error: {ex.Message}" });
        }
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request,
        [FromServices] GroqAiService groqAi
    )
    {
        Console.WriteLine("🔔 CHAT ENDPOINT CALLED!");
        
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Console.WriteLine("❌ Message is empty");
            return BadRequest(new { error = "Message is required" });
        }

        Console.WriteLine($"📨 Sending message to Groq: {request.Message}");
        var aiResponse = await groqAi.ChatAsync(request.Message);

        if (string.IsNullOrEmpty(aiResponse))
        {
            Console.WriteLine("❌ No response from Groq");
            return BadRequest(new { error = "Failed to get response from AI" });
        }

        Console.WriteLine($"✨ Returning response: {aiResponse}");
        return Ok(new { response = aiResponse });
    }
}
