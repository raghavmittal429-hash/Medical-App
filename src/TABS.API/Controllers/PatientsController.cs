using Microsoft.AspNetCore.Mvc;
using TABS.API.Application;
using TABS.Core.Models;

namespace TABS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientAnalysisOrchestrator _orchestrator;
    private readonly ILogger<PatientsController> _logger;

    public PatientsController(
        IPatientAnalysisOrchestrator orchestrator,
        ILogger<PatientsController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost("{patientId:guid}/upload")]
    public async Task<ActionResult<MedicalRecord>> UploadDocument(Guid patientId, IFormFile file)
    {
        try
        {
            var record = await _orchestrator.UploadRecordAsync(patientId, file);

            return Ok(record);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
        var profile = await _orchestrator.GetTemporalProfileAsync(patientId);
        return Ok(profile);
    }

    [HttpGet("{patientId:guid}/causal-graph")]
    public async Task<ActionResult<CausalGraph>> GetCausalGraph(Guid patientId, [FromQuery] string target = "current_condition")
    {
        var graph = await _orchestrator.GetCausalGraphAsync(patientId, target);
        return Ok(graph);
    }

    [HttpGet("{patientId:guid}/suggestions")]
    public async Task<ActionResult<ClinicalSuggestion>> GetSuggestions(Guid patientId)
    {
        var suggestion = await _orchestrator.GetSuggestionAsync(patientId);
        return Ok(suggestion);
    }

    [HttpPost("simplify")]
    public async Task<ActionResult<SimplifiedExplanation>> SimplifyText([FromBody] SimplifyRequest request)
    {
        var result = await _orchestrator.SimplifyTextAsync(request.Text, request.Language, request.ReadingLevel);
        return Ok(result);
    }
}

public class SimplifyRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public int ReadingLevel { get; set; } = 8;
}
