using Spectre.Console;

namespace optimizerDuck.UI.Logger;

public static class GlobalStatus
{
    public static StatusContext? Current { get; set; }
}