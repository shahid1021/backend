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
            return BadRequest(new { error = "Failed to generate DFD guidance. The AI service may be temporarily unavailable. Please try again." });
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
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DetectDuplicate(
        [FromForm] IFormFile file,
        [FromServices] AppDbContext db,
        [FromServices] GroqAiService groqAi,
        [FromServices] FileTextExtractor extractor
    )
    {
        Console.WriteLine("🔍 DETECT DUPLICATE ENDPOINT CALLED!");

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        try
        {
            // 1️⃣ Save file temporarily and extract text properly (PdfPig/OpenXml)
            var tempPath = Path.Combine(Path.GetTempPath(), file.FileName);
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var uploadedText = extractor.ExtractText(tempPath);
            System.IO.File.Delete(tempPath);

            if (string.IsNullOrWhiteSpace(uploadedText))
                return BadRequest(new { error = "Could not extract text from file. Please upload a PDF or DOCX." });

            Console.WriteLine($"📄 Extracted {uploadedText.Length} chars from {file.FileName}");

            // 2️⃣ Get ALL projects from database that have abstractions
            var allProjects = db.Projects
                .Where(p => p.Abstraction != null && p.Abstraction != "")
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

            if (allProjects.Count == 0)
            {
                return Ok(new
                {
                    isDuplicate = false,
                    similarProjects = new List<object>(),
                    newFeatures = new List<string>(),
                    recommendation = "",
                    analysis = "No existing projects in database to compare against. This is a unique project!",
                    debugExtractedText = uploadedText,
                    debugDatabaseAbstractions = allProjects.Select(p => new { p.ProjectId, p.Title, p.Abstraction }).ToList()
                });
            }

            // STRICT DUPLICATE CHECK: Normalized exact text match
            string Normalize(string text)
            {
                if (string.IsNullOrEmpty(text)) return "";
                var sb = new System.Text.StringBuilder();
                foreach (var c in text.ToLowerInvariant())
                {
                    if (char.IsLetterOrDigit(c)) sb.Append(c);
                }
                return sb.ToString();
            }

            var normalizedUploaded = Normalize(uploadedText);
            var exactMatch = allProjects.FirstOrDefault(p => Normalize(p.Abstraction ?? "") == normalizedUploaded);

            // Debug logging for troubleshooting
            if (exactMatch == null)
            {
                Console.WriteLine("==== DUPLICATE CHECK DEBUG ====");
                Console.WriteLine($"Uploaded (normalized): {normalizedUploaded}");
                foreach (var p in allProjects)
                {
                    Console.WriteLine($"DB Project: {p.Title}");
                    Console.WriteLine($"Abstraction (normalized): {Normalize(p.Abstraction ?? "")}");
                }
                Console.WriteLine("==== END DEBUG ====");
            }

            if (exactMatch != null)
            {
                return Ok(new
                {
                    isDuplicate = true,
                    similarProjects = new List<object>
                    {
                        new {
                            name = exactMatch.Title ?? "Unknown",
                            batch = exactMatch.Batch ?? "N/A",
                            group = exactMatch.TeamMembers ?? "N/A",
                            createdBy = exactMatch.CreatedBy ?? "N/A",
                            similarity = 100,
                            reason = "Exact match found. This project is already done.",
                            dateCompleted = exactMatch.DateCompleted
                        }
                    },
                    newFeatures = new List<string>(),
                    recommendation = "",
                    analysis = "Already Done! This project is identical to an existing one.",
                    totalChecked = allProjects.Count,
                    debugExtractedText = uploadedText,
                    debugDatabaseAbstractions = allProjects.Select(p => new { p.ProjectId, p.Title, p.Abstraction }).ToList()
                });
            }

            // 3️⃣ Use AI to compare against each project (batch up to 5 at a time for context)
            var results = new List<object>();

            foreach (var project in allProjects)
            {
                var existingText = $"{project.Title}\n{project.Abstraction}";
                var aiResult = await groqAi.CheckSimilarityAsync(
                    uploadedText.Length > 3000 ? uploadedText.Substring(0, 3000) : uploadedText,
                    existingText,
                    project.Title ?? ""
                );

                if (aiResult != null)
                {
                    try
                    {
                        var cleanJson = aiResult.Trim();
                        if (cleanJson.StartsWith("```"))
                        {
                            cleanJson = cleanJson.Substring(cleanJson.IndexOf('{'));
                            cleanJson = cleanJson.Substring(0, cleanJson.LastIndexOf('}') + 1);
                        }

                        var parsed = JsonDocument.Parse(cleanJson);
                        var similarity = parsed.RootElement.GetProperty("similarity").GetInt32();
                        var reason = parsed.RootElement.GetProperty("reason").GetString() ?? "";

                        if (similarity >= 25)
                        {
                            results.Add(new
                            {
                                name = project.Title ?? "Unknown",
                                batch = project.Batch ?? "N/A",
                                group = project.TeamMembers ?? "N/A",
                                createdBy = project.CreatedBy ?? "N/A",
                                similarity = similarity,
                                reason = reason,
                                dateCompleted = project.DateCompleted
                            });
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Console.WriteLine($"⚠️ Could not parse AI response for {project.Title}: {parseEx.Message}");
                    }
                }
            }

            // Sort by similarity descending
            results = results.OrderByDescending(r => ((dynamic)r).similarity).ToList();

            bool isDuplicate = results.Count > 0;

            // 4️⃣ If matches found, ask AI to identify unique features
            var newFeatures = new List<string>();
            string aiSummary = "";
            string recommendation = "";

            if (isDuplicate)
            {
                var matchedSummaries = allProjects
                    .Where(p => results.Any(r => ((dynamic)r).name == p.Title))
                    .Select(p => $"Title: {p.Title}\nAbstract: {p.Abstraction ?? "N/A"}")
                    .Take(5)
                    .ToList();

                if (matchedSummaries.Count > 0)
                {
                    Console.WriteLine("🤖 Asking AI to analyze new features...");
                    var truncatedUpload = uploadedText.Length > 3000 ? uploadedText.Substring(0, 3000) : uploadedText;
                    var aiFeatureResult = await groqAi.AnalyzeNewFeaturesAsync(truncatedUpload, matchedSummaries);

                    if (aiFeatureResult != null)
                    {
                        try
                        {
                            var cleanJson = aiFeatureResult.Trim();
                            if (cleanJson.StartsWith("```"))
                            {
                                cleanJson = cleanJson.Substring(cleanJson.IndexOf('{'));
                                cleanJson = cleanJson.Substring(0, cleanJson.LastIndexOf('}') + 1);
                            }

                            var parsed = JsonDocument.Parse(cleanJson);

                            if (parsed.RootElement.TryGetProperty("newFeatures", out var featuresEl))
                            {
                                foreach (var f in featuresEl.EnumerateArray())
                                {
                                    var featureText = f.GetString();
                                    if (!string.IsNullOrWhiteSpace(featureText))
                                        newFeatures.Add(featureText);
                                }
                            }

                            if (parsed.RootElement.TryGetProperty("summary", out var summaryEl))
                                aiSummary = summaryEl.GetString() ?? "";

                            if (parsed.RootElement.TryGetProperty("recommendation", out var recEl))
                                recommendation = recEl.GetString() ?? "";
                        }
                        catch (Exception parseEx)
                        {
                            Console.WriteLine($"⚠️ Could not parse AI features: {parseEx.Message}");
                        }
                    }
                }
            }

            string analysis = isDuplicate
                ? (!string.IsNullOrEmpty(aiSummary) ? aiSummary : $"Found {results.Count} similar project(s) in the database.")
                : "No similar projects found. This appears to be a unique project!";

            Console.WriteLine($"✅ Result: {results.Count} similar project(s) found");

            return Ok(new
            {
                isDuplicate = isDuplicate,
                similarProjects = results,
                newFeatures = newFeatures,
                recommendation = recommendation,
                analysis = analysis,
                totalChecked = allProjects.Count,
                debugExtractedText = uploadedText,
                debugDatabaseAbstractions = allProjects.Select(p => new { p.ProjectId, p.Title, p.Abstraction }).ToList()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Exception: {ex.Message}");
            return BadRequest(new { error = $"Error: {ex.Message}" });
        }
    }

    // ==================== AI-POWERED SIMILARITY CHECK ====================
    [HttpPost("ai-similarity")]
    public async Task<IActionResult> AiSimilarityCheck(
        [FromBody] DfdRequest request,
        [FromServices] AppDbContext db,
        [FromServices] GroqAiService groqAi
    )
    {
        Console.WriteLine("🤖 AI SIMILARITY CHECK ENDPOINT CALLED!");

        if (string.IsNullOrWhiteSpace(request.AbstractText))
            return BadRequest(new { error = "Abstract is required" });

        try
        {
            var uploadedText = request.AbstractText;

            // Decode base64 if needed
            try
            {
                byte[] data = Convert.FromBase64String(uploadedText);
                uploadedText = System.Text.Encoding.UTF8.GetString(data);
            }
            catch { /* Not base64, use as-is */ }

            // Get all projects with abstractions
            var allProjects = db.Projects
                .Where(p => p.Abstraction != null && p.Abstraction != "")
                .Select(p => new { p.ProjectId, p.Title, p.Abstraction, p.Batch, p.CreatedBy, p.TeamMembers })
                .ToList();

            Console.WriteLine($"📊 Checking against {allProjects.Count} projects with AI");

            var results = new List<object>();

            foreach (var project in allProjects)
            {
                var aiResult = await groqAi.CheckSimilarityAsync(
                    uploadedText,
                    project.Abstraction ?? "",
                    project.Title ?? ""
                );

                if (aiResult != null)
                {
                    try
                    {
                        var parsed = System.Text.Json.JsonDocument.Parse(aiResult);
                        var similarity = parsed.RootElement.GetProperty("similarity").GetInt32();
                        var reason = parsed.RootElement.GetProperty("reason").GetString();

                        if (similarity >= 20) // Only include if 20%+ similarity
                        {
                            results.Add(new
                            {
                                name = project.Title ?? "Unknown",
                                batch = project.Batch ?? "N/A",
                                group = project.TeamMembers ?? "N/A",
                                createdBy = project.CreatedBy ?? "N/A",
                                similarity = similarity,
                                reason = reason
                            });
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"⚠️ Could not parse AI response for {project.Title}");
                    }
                }
            }

            // Sort by similarity descending
            results = results.OrderByDescending(r => ((dynamic)r).similarity).ToList();

            return Ok(new
            {
                isDuplicate = results.Count > 0,
                similarProjects = results,
                totalChecked = allProjects.Count,
                analysis = results.Count > 0
                    ? $"AI found {results.Count} similar project(s)"
                    : "AI confirms this is a unique project!"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Exception: {ex.Message}");
            return BadRequest(new { error = $"Error: {ex.Message}" });
        }
    }
}
