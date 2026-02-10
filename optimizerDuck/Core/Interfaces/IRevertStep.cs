using Newtonsoft.Json.Linq;

namespace optimizerDuck.Core.Interfaces;

public interface IRevertStep
{
    public string Type { get; }

    Task<bool> ExecuteAsync();

    JObject ToData();
}