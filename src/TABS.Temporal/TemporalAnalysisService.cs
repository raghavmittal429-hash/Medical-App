using TABS.Core.Models;
using TABS.Core.Persistence;

namespace TABS.Temporal.Services;

public interface ITemporalAnalysisService
{
    Task<TemporalProfile> AnalyzePatientHistoryAsync(Guid patientId);
    Task<List<Anomaly>> DetectAnomaliesAsync(List<DataPoint> timeSeries, string variableName);
    Task<Trajectory> PredictTrajectoryAsync(List<DataPoint> historicalData, string variableName, int daysAhead = 90);
}

public class DynamicBayesianNetworkService : ITemporalAnalysisService
{
    private readonly IRepository<MedicalRecord> _recordRepository;

    public DynamicBayesianNetworkService(IRepository<MedicalRecord> recordRepository)
    {
        _recordRepository = recordRepository;
    }

    public async Task<TemporalProfile> AnalyzePatientHistoryAsync(Guid patientId)
    {
        var records = await _recordRepository.GetAllAsync(r => r.PatientId == patientId);
        var profile = new TemporalProfile();

        var labGroups = records
            .SelectMany(r => r.ExtractedData.LabValues)
            .GroupBy(l => l.NormalizedName);

        foreach (var group in labGroups)
        {
            var timeSeries = group
                .OrderBy(l => l.TestDate)
                .Select(l => new DataPoint { Timestamp = l.TestDate, Value = l.Value })
                .ToList();

            if (timeSeries.Count < 2)
            {
                continue;
            }

            var trend = AnalyzeTrend(timeSeries, group.Key);
            profile.Trends.Add(trend);

            var anomalies = await DetectAnomaliesAsync(timeSeries, group.Key);
            profile.DetectedAnomalies.AddRange(anomalies);

            var trajectory = await PredictTrajectoryAsync(timeSeries, group.Key);
            profile.Trajectories[group.Key] = trajectory;
        }

        return profile;
    }

    public Task<List<Anomaly>> DetectAnomaliesAsync(List<DataPoint> timeSeries, string variableName)
    {
        var anomalies = new List<Anomaly>();
        if (timeSeries.Count < 3)
        {
            return Task.FromResult(anomalies);
        }

        var mean = timeSeries.Average(d => d.Value);
        var stdDev = CalculateStdDev(timeSeries.Select(d => d.Value));
        if (stdDev == 0)
        {
            return Task.FromResult(anomalies);
        }

        foreach (var point in timeSeries)
        {
            var zScore = Math.Abs((point.Value - mean) / stdDev);
            if (zScore > 2.5)
            {
                anomalies.Add(new Anomaly
                {
                    VariableName = variableName,
                    Timestamp = point.Timestamp,
                    ActualValue = point.Value,
                    ExpectedValue = mean,
                    DeviationScore = zScore,
                    Explanation = GenerateAnomalyExplanation(variableName, point.Value, mean, zScore)
                });
            }
        }

        return Task.FromResult(anomalies);
    }

    public Task<Trajectory> PredictTrajectoryAsync(List<DataPoint> historicalData, string variableName, int daysAhead = 90)
    {
        var lastDate = historicalData.Last().Timestamp;
        var trend = AnalyzeTrend(historicalData, variableName);

        var predictions = new List<PredictedState>();
        var currentValue = historicalData.Last().Value;
        var confidence = CalculatePredictionConfidence(historicalData);

        for (var day = 30; day <= daysAhead; day += 30)
        {
            var projectedDate = lastDate.AddDays(day);
            var projectedValue = currentValue + (trend.Slope * day);
            var uncertainty = trend.Volatility * Math.Sqrt(day / 30.0);
            var denominator = Math.Abs(projectedValue) < 0.0001 ? 1.0 : Math.Abs(projectedValue);
            var probability = Math.Max(0, 1 - (uncertainty / denominator));

            predictions.Add(new PredictedState
            {
                ProjectedDate = projectedDate,
                ExpectedValue = projectedValue,
                Probability = probability,
                ContributingFactors = new List<string>
                {
                    $"Historical trend: {trend.Direction}",
                    $"Volatility: {trend.Volatility:F2}",
                    $"Data points: {historicalData.Count}"
                }
            });
        }

        return Task.FromResult(new Trajectory
        {
            VariableName = variableName,
            PredictedStates = predictions,
            Confidence = confidence
        });
    }

    private static TemporalTrend AnalyzeTrend(List<DataPoint> data, string variableName)
    {
        var n = data.Count;
        var sumX = data.Select((_, i) => (double)i).Sum();
        var sumY = data.Sum(d => d.Value);
        var sumXY = data.Select((d, i) => (double)i * d.Value).Sum();
        var sumX2 = data.Select((_, i) => (double)i * i).Sum();

        var denominator = (n * sumX2 - sumX * sumX);
        var slope = Math.Abs(denominator) < 0.0001 ? 0 : (n * sumXY - sumX * sumY) / denominator;
        var intercept = (sumY - slope * sumX) / n;

        var predicted = data.Select((_, i) => slope * i + intercept).ToList();
        var residuals = data.Select((d, i) => d.Value - predicted[i]).ToList();
        var volatility = CalculateStdDev(residuals);

        var direction = slope switch
        {
            > 0.1 => TrendDirection.Worsening,
            < -0.1 => TrendDirection.Improving,
            _ when volatility > 0.2 => TrendDirection.Fluctuating,
            _ => TrendDirection.Stable
        };

        if (IsInverseMarker(variableName))
        {
            direction = direction switch
            {
                TrendDirection.Improving => TrendDirection.Worsening,
                TrendDirection.Worsening => TrendDirection.Improving,
                _ => direction
            };
        }

        return new TemporalTrend
        {
            VariableName = variableName,
            Direction = direction,
            Slope = slope,
            Volatility = volatility,
            WindowStart = data.First().Timestamp,
            WindowEnd = data.Last().Timestamp,
            DataPoints = data
        };
    }

    private static double CalculatePredictionConfidence(List<DataPoint> data)
    {
        var n = Math.Min(data.Count / 10.0, 1.0);
        var consistency = 1.0 / (1.0 + CalculateStdDev(data.Select(d => d.Value)));
        return (n * 0.6) + (consistency * 0.4);
    }

    private static double CalculateStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var avg = list.Average();
        return Math.Sqrt(list.Average(v => Math.Pow(v - avg, 2)));
    }

    private static bool IsInverseMarker(string variableName)
    {
        var inverseMarkers = new[] { "hdl", "vitamin_d", "iron", "albumin" };
        return inverseMarkers.Any(m => variableName.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateAnomalyExplanation(string variable, double actual, double expected, double zScore)
    {
        var severity = zScore switch
        {
            > 3.5 => "critically abnormal",
            > 2.5 => "significantly abnormal",
            _ => "moderately abnormal"
        };

        var direction = actual > expected ? "elevated" : "depressed";
        return $"{variable} is {severity} ({direction}). Expected: {expected:F2}, Actual: {actual:F2}. Deviation: {zScore:F1} standard deviations.";
    }
}
