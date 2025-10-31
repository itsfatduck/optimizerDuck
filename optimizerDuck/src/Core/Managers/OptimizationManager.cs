using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Extensions;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Optimizers;
using optimizerDuck.Core.Services;
using optimizerDuck.Interfaces;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;
using Spectre.Console;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace optimizerDuck.Core.Managers;

public class OptimizationManager(SystemSnapshot systemSnapshot)
{
    private static readonly ILogger Log = Logger.CreateLogger<OptimizationManager>();
    private Queue<OptimizationTweakChoice> _selectedTweaks = new();
    private SystemSnapshot _systemSnapshot = systemSnapshot;

    public static Queue<string> SelectedBloatware { get; private set; } =
        new(); // public and static for Remove Bloatware Apps tweak


    private static List<OptimizationGroupChoice> GetTweakChoices()
    {
        return CacheManager.GetOrCreate("optimization_choices", entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;

            var groups = new List<OptimizationGroupChoice>();
            var optimizationGroups = ReflectionHelper.FindImplementationsInLoadedAssemblies(typeof(IOptimizationGroup));

            var enumerable = optimizationGroups as Type[] ?? optimizationGroups.ToArray();
            var allTweaks = enumerable
                .SelectMany(og => og.GetNestedTypes(BindingFlags.Public)
                    .Where(t => typeof(IOptimizationTweak).IsAssignableFrom(t))
                    .Select(t => (IOptimizationTweak?)Activator.CreateInstance(t)!))
                .Where(_ => true)
                .ToList();

            var globalMaxNameLength = allTweaks.Count != 0
                ? allTweaks.Max(t => t.Name.Length) + 1
                : 0;


            foreach (var optimizationGroup in enumerable)
            {
                var groupInstance = (IOptimizationGroup)Activator.CreateInstance(optimizationGroup)!;

                var tweaks = optimizationGroup
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t => typeof(IOptimizationTweak).IsAssignableFrom(t))
                    .Select(t => (IOptimizationTweak)Activator.CreateInstance(t)!)
                    .OrderByDescending(t => t.EnabledByDefault)
                    .ToList();

                if (tweaks.Count == 0) // skip groups with no tweaks
                    continue;

                var tweakChoices = tweaks
                    .Select(t =>
                    {
                        var paddedName = t.Name.PadRight(globalMaxNameLength);
                        var description = $"[dim]| {t.Description}[/]";

                        return new OptimizationTweakChoice(t, paddedName, description, t.EnabledByDefault);
                    })
                    .ToList();

                groups.Add(new OptimizationGroupChoice(groupInstance.Name, groupInstance.Priority, tweakChoices));
            }

            var orderedGroups = groups.OrderBy(g => g.Priority).ToList();
            foreach (var g in orderedGroups)
            {
                Log.LogDebug("Loaded group {Group}:", g.Name);
                foreach (var t in g.Tweaks)
                    if (t.Description != null)
                        Log.LogDebug("  - {TweakName} {TweakDescription}", t.Name, Markup.Remove(t.Description));
            }

            return orderedGroups;
        });
    }


    public async Task<bool> Begin()
    {
        SystemHelper.Title("Select the tweaks you want to apply");
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        Log.LogInformation("Loading optimizations...");

        var optimizationGroups = GetTweakChoices();
        Log.LogInformation("Loaded {GroupAmount} groups with {TweakAmount} tweaks.", optimizationGroups.Count,
            optimizationGroups.Sum(g => g.Tweaks.Count));

        try
        {
            using var escapeCancellableConsole = new EscapeCancellableConsole(AnsiConsole.Console);
            var promptTweakChoice = new MultiSelectionPrompt<OptimizationTweakChoice>()
                .Title("Select the tweaks you want to apply")
                .InstructionsText(Defaults.EscapeCancellableConsoleMultiSelectionPromptInstructionsText)
                .Required(true)
                .HighlightStyle(Theme.Primary)
                .UseConverter(t => $"{t.Name} {t.Description}")
                .PageSize(24);

            foreach (var g in optimizationGroups)
                promptTweakChoice.AddChoiceGroup(
                    new OptimizationTweakChoice(null, $"[bold underline]{g.Name}[/]", null,
                        false), // hack to add group title ok vjp pro, and this cant be selected btw
                    g.Tweaks
                );

            if (_selectedTweaks.Count == 0) // only select enabled by default if no tweaks are selected (first time)
                foreach (var tweak in optimizationGroups.SelectMany(g => g.Tweaks).Where(t => t.EnabledByDefault))
                    promptTweakChoice.Select(tweak);
            else
                foreach (var selectedTweak in _selectedTweaks.ToList())
                    promptTweakChoice.Select(selectedTweak);

            var optimizationTweakChoices =
                await escapeCancellableConsole.PromptAsync(promptTweakChoice).ConfigureAwait(false);

            _selectedTweaks = new Queue<OptimizationTweakChoice>(optimizationTweakChoices);

            if (_selectedTweaks.Any(t =>
                    t.Instance?.GetType() ==
                    typeof(BloatwareAndServices.RemoveBloatwareApps))) // if Bloatware selection is selected
            {
                var promptBloatware = new MultiSelectionPrompt<string>()
                    .Title("Select the bloatware you want to remove")
                    .InstructionsText(Defaults.EscapeCancellableConsoleMultiSelectionPromptInstructionsText)
                    .Required(true)
                    .HighlightStyle(Theme.Primary)
                    .UseConverter(key =>
                        $"{(key.Contains("Safe") || key.Contains("Caution") ? key + " " : "")}{Defaults.SAFE_APPS.GetValueOrDefault(key) ?? Defaults.CAUTION_APPS.GetValueOrDefault(key)}")
                    .AddChoiceGroup("[bold underline lightgreen]Safe Apps[/] - [lightgreen]Recommended[/]",
                        Defaults.SAFE_APPS.Keys)
                    .AddChoiceGroup("[bold underline red]Caution Apps[/] - [red]May cause issues[/]",
                        Defaults.CAUTION_APPS.Keys)
                    .PageSize(24);

                if (SelectedBloatware is { Count: > 0 })
                    foreach (var app in SelectedBloatware)
                        promptBloatware.Select(app);
                else
                    foreach (var app in Defaults.SAFE_APPS.Keys)
                        promptBloatware.Select(app);


                SelectedBloatware =
                    new Queue<string>(await escapeCancellableConsole.PromptAsync(promptBloatware)
                        .ConfigureAwait(false));
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return true;
    }

    public static Task<bool> RestorePoint()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        SystemHelper.Title("Setting up");

        return Task.FromResult(PromptDialog.Warning("Restore Point Before Continuing",
            $"""
             We [{Theme.Success}]RECOMMEND[/] you to create a Restore Point before continuing.
             This way, you will be [{Theme.Success}]protected[/] against system-level changes and can always revert them if needed.
             """,
            new PromptOption("Yes", Theme.Success, () =>
            {
                if (SystemProtectionHelper.Enable()) // if we can enable, create
                    SystemProtectionHelper.Create();
            }),
            new PromptOption("Skip", Theme.Error, () => true),
            new PromptOption("Back", Theme.Warning, () => false)));
    }

    public async Task Run()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);
        SystemHelper.Title("Initializing Optimizer");

        Log.LogInformation("Refreshing system information...");
        _systemSnapshot = await SystemInfoService.RefreshAsync().ConfigureAwait(false);

        Log.LogDebug("Selected tweaks ({TweakAmount}): {Tweaks}", _selectedTweaks.Count,
            string.Join(", ", _selectedTweaks.Select(t => t.Name.Trim())));
        if (SelectedBloatware.Count != 0)
            Log.LogDebug("Selected bloatware apps ({BloatwareAmount}): {Bloatware}", SelectedBloatware.Count,
                string.Join(", ", SelectedBloatware));

        Log.LogDebug(new string('=', 60));
        while (_selectedTweaks.TryDequeue(out var selectedTweak))
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle($"bold {Theme.Primary}")
                .StartAsyncGlobal($"Applying [{Theme.Primary}]{selectedTweak.Name.Trim()}[/]...", async ctx =>
                {
                    var tweakName = selectedTweak.Name.Trim();
                    try
                    {
                        SystemHelper.Title(tweakName);
                        ctx.Status($"Applying [{Theme.Primary}]{tweakName}[/]...");

                        using var scope = Log.BeginScope("Tweak: {TweakName}", tweakName);

                        Rule($"[bold white] {tweakName} [/]", Theme.Success);
                        Log.LogInformation("Applying {TweakName}...", tweakName);

                        var stopwatch = Stopwatch.StartNew();

                        await selectedTweak.Instance!.Apply(_systemSnapshot).ConfigureAwait(false);

                        stopwatch.Stop();

                        Log.LogDebug("Finished tweak in {Elapsed}ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        Log.LogError(e, "[{Error}]Failed to apply tweak {TweakName}[/], skipped.", Theme.Error,
                            tweakName);
                    }
                    finally
                    {
                        Log.LogDebug(new string('=', 60));
                    }
                });

        Rule("Optimization completed!", Theme.Success);

        Log.LogInformation($"[{Theme.Success}]Optimization completed![/]");

        PromptDialog.Warning("Restart To Apply Changes",
            $"""
             The tweaks have been applied successfully.  
             Please [{Theme.Success}]restart[/] your PC so the changes can work properly.
             """,
            new PromptOption("Restart now", Theme.Success, () =>
            {
                Log.LogInformation("Restarting...");
                ShellService.CMD("shutdown /r /t 0");
            }),
            new PromptOption("I will restart later", Theme.Warning)
        );
    }

    private static void Rule(string title, string ruleColor)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule(title)
            .RuleStyle(ruleColor));
        AnsiConsole.WriteLine();
    }
}