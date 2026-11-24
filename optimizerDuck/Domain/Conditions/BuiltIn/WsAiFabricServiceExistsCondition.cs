using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Conditions;

/// <summary>Requires the Windows AI Components Host service (<c>WSAIFabricSvc</c>) to be installed.</summary>
public sealed class WsAiFabricServiceExistsCondition : ServiceExistsCondition
{
    protected override string ServiceName => "WSAIFabricSvc";
    protected override Func<string> Title => () => Loc.Instance["Condition.WsAiFabric.Title"];
    protected override Func<string> Description =>
        () => Loc.Instance["Condition.WsAiFabric.Description"];
}
