using TABS.Causal.Services;
using TABS.Core.Models;
using TABS.Core.Persistence;
using TABS.NLP.Services;
using TABS.OCR.Services;
using TABS.Temporal.Services;

namespace TABS.API.Application;

public class PatientAnalysisOrchestrator : IPatientAnalysisOrchestrator
{
    private readonly IOCRService _ocrService;
    private readonly ISimplificationService _simplificationService;
    private readonly ITemporalAnalysisService _temporalService;
    private readonly ICausalInferenceService _causalService;
    private readonly IRepository<MedicalRecord> _recordRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRecordTypeDetector _recordTypeDetector;

    public PatientAnalysisOrchestrator(
        IOCRService ocrService,
        ISimplificationService simplificationService,
        ITemporalAnalysisService temporalService,
        ICausalInferenceService causalService,
        IRepository<MedicalRecord> recordRepository,
        IRepository<Patient> patientRepository,
        IRecordTypeDetector recordTypeDetector)
    {
        _ocrService = ocrService;
        _simplificationService = simplificationService;
        _temporalService = temporalService;
        _causalService = causalService;
        _recordRepository = recordRepository;
        _patientRepository = patientRepository;
        _recordTypeDetector = recordTypeDetector;
    }

    public async Task<MedicalRecord> UploadRecordAsync(Guid patientId, IFormFile file)
    {
        var patient = await EnsurePatientAsync(patientId);

        await using var stream = file.OpenReadStream();
        var result = await _ocrService.ExtractMedicalDataAsync(stream, file.ContentType);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }

        var record = new MedicalRecord
        {
            PatientId = patientId,
            RecordDate = DateTime.UtcNow,
            Type = _recordTypeDetector.Detect(file.FileName),
            RawContent = result.RawExtraction,
            ExtractedData = result.StructuredData,
            ConfidenceScore = result.ConfidenceScore
        };

        await _recordRepository.AddAsync(record);
        patient.MedicalHistory.Add(record);
        return record;
    }

    public async Task<TemporalProfile> GetTemporalProfileAsync(Guid patientId)
    {
        await EnsurePatientAsync(patientId);
        return await _temporalService.AnalyzePatientHistoryAsync(patientId);
    }

    public async Task<CausalGraph> GetCausalGraphAsync(Guid patientId, string target)
    {
        await EnsurePatientAsync(patientId);
        return await _causalService.BuildCausalGraphAsync(patientId, target);
    }

    public async Task<ClinicalSuggestion> GetSuggestionAsync(Guid patientId)
    {
        await EnsurePatientAsync(patientId);

        var temporalProfile = await _temporalService.AnalyzePatientHistoryAsync(patientId);
        var causalGraph = await _causalService.BuildCausalGraphAsync(patientId, "health_optimization");
        var suggestion = await _causalService.GenerateSuggestionAsync(causalGraph, temporalProfile);

        var simplified = await _simplificationService.SimplifyMedicalContentAsync(suggestion.Description, targetLanguage: "en");
        suggestion.SimplifiedExplanation = simplified.SimplifiedText;

        return suggestion;
    }

    public Task<SimplifiedExplanation> SimplifyTextAsync(string text, string language, int readingLevel)
    {
        return _simplificationService.SimplifyMedicalContentAsync(text, language, readingLevel);
    }

    private async Task<Patient> EnsurePatientAsync(Guid patientId)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId);
        if (patient != null)
        {
            return patient;
        }

        patient = new Patient
        {
            Id = patientId,
            Name = "Demo Patient",
            DateOfBirth = DateTime.UtcNow.AddYears(-40)
        };

        await _patientRepository.AddAsync(patient);
        return patient;
    }
}
