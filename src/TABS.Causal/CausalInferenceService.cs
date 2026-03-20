using TABS.Core.Models;
using TABS.Core.Persistence;
using TABS.Temporal.Services;

namespace TABS.Causal.Services;

public interface ICausalInferenceService
{
    Task<CausalGraph> BuildCausalGraphAsync(Guid patientId, string targetOutcome);
    Task<ClinicalSuggestion> GenerateSuggestionAsync(CausalGraph graph, TemporalProfile profile);
    Task<List<CounterfactualScenario>> GenerateCounterfactualsAsync(CausalGraph graph, string intervention);
}

public class BayesianNetworkService : ICausalInferenceService
{
    private readonly ITemporalAnalysisService _temporalService;
    private readonly IRepository<Patient> _patientRepository;
    private readonly HttpClient _httpClient;

    public BayesianNetworkService(
        ITemporalAnalysisService temporalService,
        IRepository<Patient> patientRepository,
        HttpClient httpClient)
    {
        _temporalService = temporalService;
        _patientRepository = patientRepository;
        _httpClient = httpClient;
    }

    public async Task<CausalGraph> BuildCausalGraphAsync(Guid patientId, string targetOutcome)
    {
        var patient = await _patientRepository.GetByIdAsync(patientId) ?? new Patient { Id = patientId };
        var temporalProfile = await _temporalService.AnalyzePatientHistoryAsync(patientId);

        var rootNode = new CausalNode
        {
            Id = "n1",
            Label = "Glycemic Control",
            Type = NodeType.RootCause,
            CurrentValue = patient.MedicalHistory.SelectMany(r => r.ExtractedData.LabValues).LastOrDefault()?.Value ?? 0,
            Probability = 0.7
        };

        var intermediate = new CausalNode
        {
            Id = "n2",
            Label = "Inflammation Burden",
            Type = NodeType.Intermediate,
            CurrentValue = 0.6,
            Probability = 0.55
        };

        var outcome = new CausalNode
        {
            Id = "n3",
            Label = targetOutcome,
            Type = NodeType.Outcome,
            CurrentValue = 0.5,
            Probability = 0.6
        };

        var graph = new CausalGraph
        {
            TargetVariable = targetOutcome,
            GeneratedAt = DateTime.UtcNow,
            Nodes = new List<CausalNode> { rootNode, intermediate, outcome },
            Edges = new List<CausalEdge>
            {
                new()
                {
                    SourceId = "n1",
                    TargetId = "n2",
                    Strength = 0.72,
                    RelationshipType = "causes",
                    Explanation = "Poor glycemic control increases inflammatory markers"
                },
                new()
                {
                    SourceId = "n2",
                    TargetId = "n3",
                    Strength = 0.68,
                    RelationshipType = "causes",
                    Explanation = "Inflammation contributes to worsening outcomes"
                }
            }
        };

        return await ApplyBayesianInference(graph, patient, temporalProfile);
    }

    public async Task<ClinicalSuggestion> GenerateSuggestionAsync(CausalGraph graph, TemporalProfile profile)
    {
        var interventionPoints = graph.Nodes
            .Where(n => n.Type == NodeType.RootCause || n.Type == NodeType.Intermediate)
            .OrderByDescending(n => CalculateInterventionImpact(n, graph))
            .Take(2)
            .ToList();

        if (interventionPoints.Count == 0)
        {
            interventionPoints.Add(new CausalNode
            {
                Id = "fallback",
                Label = "Lifestyle factors",
                Type = NodeType.Intermediate,
                Probability = 0.5
            });
        }

        var suggestions = interventionPoints
            .Select(point =>
            {
                var impact = CalculateInterventionImpact(point, graph);
                return $"Address {point.Label} (projected impact: {impact:P0})";
            })
            .ToList();

        var causalChains = interventionPoints
            .Select(point => string.Join(" -> ", TraceCausalChain(point, graph)))
            .ToList();

        var counterfactuals = await GenerateCounterfactualsAsync(graph, interventionPoints.First().Label);

        return new ClinicalSuggestion
        {
            Title = $"Intervention Strategy for {graph.TargetVariable}",
            Description = string.Join("\n", suggestions),
            SimplifiedExplanation = string.Join(" ", suggestions),
            Priority = DeterminePriority(profile),
            SupportingEvidence = causalChains,
            CausalReasoning = graph,
            Counterfactuals = counterfactuals,
            GeneratedAt = DateTime.UtcNow
        };
    }

    public Task<List<CounterfactualScenario>> GenerateCounterfactualsAsync(CausalGraph graph, string intervention)
    {
        var scenarios = new List<CounterfactualScenario>
        {
            new()
            {
                Scenario = $"If {intervention} had been optimized 6 months ago",
                ProbabilityChange = -0.35,
                OutcomeDifference = "Current symptoms would likely be less severe"
            },
            new()
            {
                Scenario = "If current trajectory continues without intervention",
                ProbabilityChange = 0.25,
                OutcomeDifference = "Higher probability of progression in 12 months"
            },
            new()
            {
                Scenario = $"If {intervention} is optimized immediately and maintained",
                ProbabilityChange = -0.55,
                OutcomeDifference = "High chance of improved control in 6 months"
            }
        };

        return Task.FromResult(scenarios);
    }

    private static double CalculateInterventionImpact(CausalNode node, CausalGraph graph)
    {
        var outgoingEdges = graph.Edges.Where(e => e.SourceId == node.Id);
        var totalStrength = outgoingEdges.Sum(e => e.Strength);
        return Math.Clamp(totalStrength * (1 - node.Probability), 0, 1);
    }

    private static List<string> TraceCausalChain(CausalNode startNode, CausalGraph graph)
    {
        var chain = new List<string> { startNode.Label };
        var current = startNode;

        while (true)
        {
            var nextEdge = graph.Edges
                .Where(e => e.SourceId == current.Id)
                .OrderByDescending(e => e.Strength)
                .FirstOrDefault();

            if (nextEdge == null)
            {
                break;
            }

            var nextNode = graph.Nodes.FirstOrDefault(n => n.Id == nextEdge.TargetId);
            if (nextNode == null)
            {
                break;
            }

            chain.Add($"{nextEdge.RelationshipType} {nextNode.Label}");
            current = nextNode;
        }

        return chain;
    }

    private static Task<CausalGraph> ApplyBayesianInference(CausalGraph graph, Patient patient, TemporalProfile profile)
    {
        var evidenceFactor = profile.Trends.Count == 0 ? 0.5 : Math.Clamp(profile.Trends.Count / 10.0, 0.5, 0.9);

        foreach (var node in graph.Nodes)
        {
            var prior = node.Probability;
            var likelihood = Math.Clamp(evidenceFactor, 0.1, 0.9);
            node.Probability = (likelihood * prior) / ((likelihood * prior) + ((1 - likelihood) * (1 - prior)));
        }

        return Task.FromResult(graph);
    }

    private static SuggestionPriority DeterminePriority(TemporalProfile profile)
    {
        var criticalAnomalies = profile.DetectedAnomalies.Count(a => a.DeviationScore > 3.0);
        var worseningTrends = profile.Trends.Count(t => t.Direction == TrendDirection.Worsening);

        if (criticalAnomalies > 0 || worseningTrends > 2)
        {
            return SuggestionPriority.Critical;
        }

        if (worseningTrends > 0)
        {
            return SuggestionPriority.High;
        }

        if (profile.Trends.Any(t => t.Direction == TrendDirection.Fluctuating))
        {
            return SuggestionPriority.Medium;
        }

        return SuggestionPriority.Low;
    }
}
