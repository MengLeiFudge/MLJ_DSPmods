using System.Collections.Generic;

namespace AfterBuildEvent.DspCalcQuickUpdate;

internal enum SourceAuditStatus {
    Passed,
    Changed,
    Uncertain,
    ConfigError,
}

internal sealed class SourceAuditResult {
    public SourceAuditStatus Status { get; }
    public string Message { get; }
    public List<string> Details { get; } = [];
    public bool CanQuickUpdate => Status == SourceAuditStatus.Passed;

    private SourceAuditResult(SourceAuditStatus status, string message, IEnumerable<string> details = null) {
        Status = status;
        Message = message;
        if (details != null) {
            Details.AddRange(details);
        }
    }

    public static SourceAuditResult Passed(string message, IEnumerable<string> details = null) {
        return new(SourceAuditStatus.Passed, message, details);
    }

    public static SourceAuditResult Changed(string message, IEnumerable<string> details = null) {
        return new(SourceAuditStatus.Changed, message, details);
    }

    public static SourceAuditResult Uncertain(string message, IEnumerable<string> details = null) {
        return new(SourceAuditStatus.Uncertain, message, details);
    }

    public static SourceAuditResult ConfigError(string message, IEnumerable<string> details = null) {
        return new(SourceAuditStatus.ConfigError, message, details);
    }
}
