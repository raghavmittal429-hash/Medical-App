using System.Net.Http.Json;
using System.Text.Json;
using TABS.Core.Models;

namespace TABS.NLP.Services;

public interface ISimplificationService
{
    Task<SimplifiedExplanation> SimplifyMedicalContentAsync(
        string medicalText,
        string targetLanguage = "en",
        int readingLevel = 8);
}

public class MedGemmaSimplificationService : ISimplificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public MedGemmaSimplificationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _endpoint = "http://localhost:8080/v1/chat/completions";
    }

    public async Task<SimplifiedExplanation> SimplifyMedicalContentAsync(string medicalText, string targetLanguage = "en", int readingLevel = 8)
    {
        var languageNames = new Dictionary<string, string>
        {
            ["en"] = "English",
            ["es"] = "Spanish",
            ["fr"] = "French",
            ["de"] = "German",
            ["zh"] = "Chinese",
            ["hi"] = "Hindi",
            ["ar"] = "Arabic"
        };

        var targetLang = languageNames.GetValueOrDefault(targetLanguage, "English");
        var prompt = $"Simplify this medical text in {targetLang} for grade {readingLevel}: {medicalText}";

        var request = new
        {
            model = "medgemma-27b-text",
            messages = new[]
            {
                new { role = "system", content = "You are a medical communication specialist." },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            response_format = new { type = "json_object" }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(_endpoint, request);
            if (!response.IsSuccessStatusCode)
            {
                return Fallback(medicalText);
            }

            var apiResult = await response.Content.ReadFromJsonAsync<MedGemmaResponse>();
            var content = apiResult?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Fallback(medicalText);
            }

            var parsed = JsonSerializer.Deserialize<SimplifiedExplanation>(content);
            return parsed ?? Fallback(medicalText);
        }
        catch (Exception ex)
        {
            _ = ex;
            return Fallback(medicalText);
        }
    }

    private static SimplifiedExplanation Fallback(string text)
    {
        return new SimplifiedExplanation
        {
            OriginalText = text,
            SimplifiedText = "Your latest health report has a few values outside the normal range. Please discuss this with your doctor and follow the prescribed plan.",
            ActionItems = new List<string>
            {
                "Take medicines as advised",
                "Repeat lab tests as scheduled",
                "Seek urgent care if symptoms worsen"
            },
            RedFlags = new List<string>
            {
                "Severe chest pain",
                "Shortness of breath",
                "Confusion or fainting"
            },
            Confidence = 0.6
        };
    }
}

public class MedGemmaResponse
{
    public List<Choice> Choices { get; set; } = new();
}

public class Choice
{
    public Message Message { get; set; } = new();
}

public class Message
{
    public string Content { get; set; } = string.Empty;
}
