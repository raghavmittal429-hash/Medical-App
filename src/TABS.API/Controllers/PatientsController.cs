using Microsoft.AspNetCore.Mvc;
using TABS.Causal.Services;
using TABS.Core.Models;
using TABS.Core.Persistence;
using TABS.NLP.Services;
using TABS.OCR.Services;
using TABS.Temporal.Services;

namespace TABS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IOCRService _ocrService;
    private readonly ISimplificationService _simplificationService;
    private readonly ITemporalAnalysisService _temporalService;
    private readonly ICausalInferenceService _causalService;
    private readonly IRepository<MedicalRecord> _recordRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        IOCRService ocrService,
        ISimplificationService simplificationService,
        ITemporalAnalysisService temporalService,
        ICausalInferenceService causalService,
        IRepository<MedicalRecord> recordRepository,
        IRepository<Patient> patientRepository,
        ILogger<PatientsController> logger)
    {
        _ocrService = ocrService;
        _simplificationService = simplificationService;
        _temporalService = temporalService;
        _causalService = causalService;
        _recordRepository = recordRepository;
        _patientRepository = patientRepository;
        _logger = logger;
    }

    [HttpPost("{patientId:guid}/upload")]
    public async Task<ActionResult<MedicalRecord>> UploadDocument(Guid patientId, IFormFile file)
    {
        try
        {
            var patient = await EnsurePatientAsync(patientId);

            await using var stream = file.OpenReadStream();
            var result = await _ocrService.ExtractMedicalDataAsync(stream, file.ContentType);
            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            var record = new MedicalRecord
            {
                PatientId = patientId,
                RecordDate = DateTime.UtcNow,
                Type = DetectRecordType(file.FileName),
                RawContent = result.RawExtraction,
                ExtractedData = result.StructuredData,
                ConfidenceScore = result.ConfidenceScore
            };

            await _recordRepository.AddAsync(record);
            patient.MedicalHistory.Add(record);

            return Ok(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");
            return StatusCode(500, "Processing failed");
        }
    }

    [HttpGet("{patientId:guid}/temporal-analysis")]
    public async Task<ActionResult<TemporalProfile>> GetTemporalAnalysis(Guid patientId)
    {
        await EnsurePatientAsync(patientId);
        var profile = await _temporalService.AnalyzePatientHistoryAsync(patientId);
        return Ok(profile);
    }

    [HttpGet("{patientId:guid}/causal-graph")]
    public async Task<ActionResult<CausalGraph>> GetCausalGraph(Guid patientId, [FromQuery] string target = "current_condition")
    {
        await EnsurePatientAsync(patientId);
        var graph = await _causalService.BuildCausalGraphAsync(patientId, target);
        return Ok(graph);
    }

    [HttpGet("{patientId:guid}/suggestions")]
    public async Task<ActionResult<ClinicalSuggestion>> GetSuggestions(Guid patientId)
    {
        await EnsurePatientAsync(patientId);

        var temporalProfile = await _temporalService.AnalyzePatientHistoryAsync(patientId);
        var causalGraph = await _causalService.BuildCausalGraphAsync(patientId, "health_optimization");
        var suggestion = await _causalService.GenerateSuggestionAsync(causalGraph, temporalProfile);

        var simplified = await _simplificationService.SimplifyMedicalContentAsync(suggestion.Description, targetLanguage: "en");
        suggestion.SimplifiedExplanation = simplified.SimplifiedText;

        return Ok(suggestion);
    }

    [HttpPost("simplify")]
    public async Task<ActionResult<SimplifiedExplanation>> SimplifyText([FromBody] SimplifyRequest request)
    {
        var result = await _simplificationService.SimplifyMedicalContentAsync(request.Text, request.Language, request.ReadingLevel);
        return Ok(result);
    }

    private static RecordType DetectRecordType(string filename)
    {
        var lower = filename.ToLowerInvariant();
        if (lower.Contains("lab")) return RecordType.LabReport;
        if (lower.Contains("prescription") || lower.Contains("rx")) return RecordType.Prescription;
        if (lower.Contains("xray") || lower.Contains("scan") || lower.Contains("mri")) return RecordType.Imaging;
        return RecordType.ClinicalNotes;
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

public class SimplifyRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public int ReadingLevel { get; set; } = 8;
}
