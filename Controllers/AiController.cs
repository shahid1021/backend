using Microsoft.AspNetCore.Mvc;
using StudentAPI.Models;
using System.Text.Json;
using StudentAPI;

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

    [HttpPost("detect-duplicate")]
    public async Task<IActionResult> DetectDuplicate(
        [FromBody] DfdRequest request,
        [FromServices] AppDbContext db,
        [FromServices] GroqAiService groqAi
    )
    {
        Console.WriteLine("🔍 DETECT DUPLICATE ENDPOINT CALLED!");

        if (string.IsNullOrWhiteSpace(request.AbstractText))
        {
            Console.WriteLine("❌ Abstract is empty");
            return BadRequest(new { error = "Abstract is required" });
        }

        try
        {
            var uploadedText = request.AbstractText;
            
            // Try to decode if it's base64 (for PDF/binary files)
            try
            {
                byte[] data = Convert.FromBase64String(uploadedText);
                uploadedText = ExtractTextFromBinary(data);
                Console.WriteLine($"📄 Decoded base64 - extracted {uploadedText.Length} characters");
            }
            catch
            {
                // Not base64, use as-is
                Console.WriteLine($"📝 Using text as-is ({uploadedText.Length} characters)");
            }
            
            uploadedText = uploadedText.ToLower();
            
            // 1️⃣ Get ALL projects from database (regardless of status)
            var allProjects = db.Projects
                .Select(p => new
                {
                    p.ProjectId,
                    p.Title,
                    p.Abstraction,
                    p.Description,
                    p.Batch,
                    p.CreatedBy,
                    p.TeamMembers,
                    p.DateCompleted
                })
                .ToList();

            Console.WriteLine($"📊 Found {allProjects.Count} projects in database");
            
            // Log all projects found
            foreach(var proj in allProjects)
            {
                Console.WriteLine($"  - {proj.Title}");
            }

            // 2️⃣ Extract keywords from uploaded text
            var uploadedKeywords = ExtractKeywords(uploadedText);
            Console.WriteLine($"📝 Uploaded keywords: {string.Join(", ", uploadedKeywords)}");

            // 3️⃣ Find similar projects using AI
            var similarProjects = new List<dynamic>();
            var matchingKeywords = new HashSet<string>();
            var newKeywords = new HashSet<string>(uploadedKeywords);

            foreach (var project in allProjects)
            {
                var projectText = $"{(project.Title ?? "")} {(project.Abstraction ?? "")} {(project.Description ?? "")}".ToLower();
                var projectKeywords = ExtractKeywords(projectText);

                // Count matching keywords
                int matchCount = 0;
                var projectMatches = new List<string>();

                foreach (var keyword in uploadedKeywords)
                {
                    if (projectText.Contains(keyword))
                    {
                        matchCount++;
                        matchingKeywords.Add(keyword);
                        projectMatches.Add(keyword);
                        newKeywords.Remove(keyword);
                    }
                }

                // If 1+ keywords match, consider it similar (was 2+)
                if (matchCount >= 1)
                {
                    int similarity = uploadedKeywords.Count > 0
                        ? (int)((matchCount / (float)uploadedKeywords.Count) * 100)
                        : 0;

                    similarProjects.Add(new
                    {
                        name = project.Title ?? "Unknown",
                        batch = project.Batch ?? "N/A",
                        group = project.TeamMembers ?? "N/A",
                        createdBy = project.CreatedBy ?? "N/A",
                        similarity = similarity,
                        matchedKeywords = projectMatches,
                        dateCompleted = project.DateCompleted
                    });
                }
            }

            Console.WriteLine($"✅ Found {similarProjects.Count} similar project(s)");
            Console.WriteLine($"🏷️ New keywords: {string.Join(", ", newKeywords)}");

            bool isDuplicate = similarProjects.Count > 0;

            var response = new
            {
                isDuplicate = isDuplicate,
                similarProjects = similarProjects,
                matchingKeywords = matchingKeywords.ToList(),
                newKeywords = newKeywords.ToList(),
                analysis = isDuplicate
                    ? $"Found {similarProjects.Count} similar project(s). Matching keywords: {string.Join(", ", matchingKeywords)}"
                    : "No similar projects found. This is a unique project!"
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Exception: {ex.Message}");
            return BadRequest(new { error = $"Error: {ex.Message}" });
        }
    }

    private List<string> ExtractKeywords(string text)
    {
        var commonWords = new HashSet<string>
        {
            "project", "new", "based", "using", "proposed", "abstract",
            "the", "a", "an", "and", "or", "is", "are", "be", "by", "in", "on", "at", "to", "for"
        };

        return text
            .Replace(System.Environment.NewLine, " ")
            .Split(new[] { ' ', '.', ',', ';', ':', '!', '?', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length >= 4 && !commonWords.Contains(word.ToLower()))
            .Distinct()
            .ToList();
    }

    private string ExtractTextFromBinary(byte[] data)
    {
        try
        {
            // Extract text from PDF by getting only readable ASCII characters
            var sb = new System.Text.StringBuilder();
            
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                
                // Keep only readable ASCII characters
                if ((b >= 48 && b <= 57) ||   // 0-9
                    (b >= 65 && b <= 90) ||   // A-Z
                    (b >= 97 && b <= 122) ||  // a-z
                    b == 32 ||                 // space
                    b == 45)                   // hyphen
                {
                    sb.Append((char)b);
                }
                // Add space for other separators
                else if (data.Length > i + 1 && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                {
                    sb.Append(" ");
                }
            }
            
            // Remove duplicate spaces
            var text = System.Text.RegularExpressions.Regex.Replace(
                sb.ToString(),
                @"\s+",
                " "
            ).Trim();
            
            Console.WriteLine($"✅ Extracted from binary: {text.Substring(0, Math.Min(100, text.Length))}");
            return text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error extracting: {ex.Message}");
            return "";
        }
    }
}
