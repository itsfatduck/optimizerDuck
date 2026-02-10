namespace optimizerDuck.Core.Models.Config;

public sealed class AppSettings
{
    public AppOptions App { get; set; } = new();
    public OptimizeOptions Optimize { get; set; } = new();

    public sealed class AppOptions
    {
        public string Language { get; set; } = "en-US";
        public string Theme { get; set; } = "Dark";
    }

    public sealed class OptimizeOptions
    {
        public int ShellTimeoutMs { get; set; } = 120000;
    }
}