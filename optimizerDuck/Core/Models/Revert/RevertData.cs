using Newtonsoft.Json.Linq;

namespace optimizerDuck.Core.Models.Revert;

public class RevertStepData
{
    public string Type { get; set; } = string.Empty;
    public JObject Data { get; set; } = new();
}

public class RevertData
{
    public Guid OptimizationId { get; set; }
    public string OptimizationName { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public List<RevertStepData> Steps { get; set; } = new();
}