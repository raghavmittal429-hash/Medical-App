using TABS.Core.Models;

namespace TABS.API.Application;

public interface IRecordTypeDetector
{
    RecordType Detect(string filename);
}

public class RecordTypeDetector : IRecordTypeDetector
{
    public RecordType Detect(string filename)
    {
        var lower = filename.ToLowerInvariant();
        if (lower.Contains("lab")) return RecordType.LabReport;
        if (lower.Contains("prescription") || lower.Contains("rx")) return RecordType.Prescription;
        if (lower.Contains("xray") || lower.Contains("scan") || lower.Contains("mri")) return RecordType.Imaging;
        return RecordType.ClinicalNotes;
    }
}
