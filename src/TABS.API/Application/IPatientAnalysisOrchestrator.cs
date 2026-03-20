using TABS.Core.Models;

namespace TABS.API.Application;

public interface IPatientAnalysisOrchestrator
{
    Task<MedicalRecord> UploadRecordAsync(Guid patientId, IFormFile file);
    Task<TemporalProfile> GetTemporalProfileAsync(Guid patientId);
    Task<CausalGraph> GetCausalGraphAsync(Guid patientId, string target);
    Task<ClinicalSuggestion> GetSuggestionAsync(Guid patientId);
    Task<SimplifiedExplanation> SimplifyTextAsync(string text, string language, int readingLevel);
}
