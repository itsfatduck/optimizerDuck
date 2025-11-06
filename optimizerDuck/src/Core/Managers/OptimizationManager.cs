using Microsoft.Extensions.Logging;
using optimizerDuck.Core.Extensions;
using optimizerDuck.Core.Helpers;
using optimizerDuck.Core.Optimizers;
using optimizerDuck.Core.Services;
using optimizerDuck.Models;
using optimizerDuck.UI;
using optimizerDuck.UI.Components;
using optimizerDuck.UI.Logger;
using Spectre.Console;
using System.Diagnostics;
using Spectre.Console.Rendering;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace optimizerDuck.Core.Managers;

public class OptimizationManager(SystemSnapshot systemSnapshot)
{
    private static readonly ILogger Log = Logger.CreateLogger<OptimizationManager>();
    private Queue<OptimizationTweakChoice> _selectedTweaks = new();
    private SystemSnapshot _systemSnapshot = systemSnapshot;

    public static Queue<AppxPackage> SelectedBloatware { get; private set; } =
        new(); // public and static for Remove Bloatware Apps tweak


    public async Task<bool> Begin()
    {
        SystemHelper.Title("Select the tweaks you want to apply");
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        Log.LogInformation("Loading optimizations...");

        var optimizationGroups = OptimizationHelper.GetTweakChoices();
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
                    new OptimizationTweakChoice(null, $"[bold underline]{g.Name}[/]", string.Empty,
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
                SystemHelper.Title("Select the bloatware you want to remove");
                Log.LogDebug("Bloatware selection detected, prompting for bloatware apps...");
                Log.LogInformation("Loading installed bloatware apps...");

                var appxClassification = OptimizationHelper.GetBloatwareChoices();

                if (appxClassification.SafeApps.Count == 0 && appxClassification.CautionApps.Count == 0)
                {
                    return PromptDialog.Warning(
                        "No Bloatware Detected",
                        $"""
                         [{Theme.Success}]Great news![/]  
                         We [{Theme.Warning}]couldn't[/] find any bloatware installed on your system.  
                         There's nothing you need to remove, so you can [{Theme.Success}]safely continue[/].
                         """,
                        new PromptOption("Continue", Theme.Success),
                        new PromptOption("Back", Theme.Warning, () => false)
                    );
                }

                var promptBloatware = new MultiSelectionPrompt<AppxPackage>()
                    .Title("Select the bloatware you want to remove")
                    .InstructionsText(Defaults.EscapeCancellableConsoleMultiSelectionPromptInstructionsText)
                    .Required(true)
                    .HighlightStyle(Theme.Primary)
                    .UseConverter(app =>
                        $"{(app.DisplayName.Contains("Safe Apps") || app.DisplayName.Contains("Caution Apps") ?
                            app.DisplayName :
                            $"[bold]{app.DisplayName}[/] [dim]{app.Version} {app.InstallLocation}[/]")
                        }")
                    .PageSize(24);

                if (appxClassification.SafeApps.Count > 0)
                    promptBloatware.AddChoiceGroup(
                        new AppxPackage
                        {
                            DisplayName = "[bold underline lightgreen]Safe Apps[/] - [lightgreen]Safe to remove[/]",
                            Name = "",
                            Version = "",
                            InstallLocation = ""
                        },
                        appxClassification.SafeApps);

                if (appxClassification.CautionApps.Count > 0)
                    promptBloatware.AddChoiceGroup(
                        new AppxPackage
                        {
                            DisplayName = "[bold underline red]Caution Apps[/] - [red]May cause issues[/]",
                            Name = "",
                            Version = "",
                            InstallLocation = ""
                        },
                        appxClassification.CautionApps);


                if (SelectedBloatware.Count == 0) // default selected safe apps first time only
                    foreach (var app in appxClassification.SafeApps)
                        promptBloatware.Select(app);
                else
                    foreach (var app in SelectedBloatware)
                        promptBloatware.Select(app);

                var bloatwareSelection =
                    await escapeCancellableConsole.PromptAsync(promptBloatware).ConfigureAwait(false);

                SelectedBloatware = new Queue<AppxPackage>(bloatwareSelection);
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

        SystemHelper.Title("Create Restore Point");

        return Task.FromResult(PromptDialog.Warning("Create Restore Point",
            $"""
             For your safety, we [{Theme.Success}]highly recommend[/] creating a system Restore Point now.
             It acts as a safeguard, allowing you to [{Theme.Success}]roll back[/] any unwanted system changes later.
             """,
            new PromptOption("Yes", Theme.Success, () =>
            {
                if (SystemProtectionHelper.Enable())
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
            string.Join(", ", _selectedTweaks.Select(t => t.Name)));
        if (SelectedBloatware.Count != 0)
            Log.LogDebug("Selected bloatware apps ({BloatwareAmount}): {Bloatware}", SelectedBloatware.Count,
                string.Join(", ", SelectedBloatware.Select(app => $"{app.DisplayName} ({app.Version})")));

        Log.LogDebug(new string('=', 60));
        while (_selectedTweaks.TryDequeue(out var selectedTweak))
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle($"bold {Theme.Primary}")
                .StartAsyncGlobal($"Applying [{Theme.Primary}]{selectedTweak.Name}[/]...", async ctx =>
                {
                    try
                    {
                        selectedTweak = selectedTweak with
                        {
                            Name = selectedTweak.Name.Trim(), Description = selectedTweak.Description.Trim()
                        };
                        SystemHelper.Title(selectedTweak.Name);
                        ctx.Status($"Applying [{Theme.Primary}]{selectedTweak.Name}[/]...");

                        using var scope = Log.BeginScope("Tweak: {TweakName}", selectedTweak.Name);


                        Rule($"[bold white] {selectedTweak.Name} [/]", Theme.Success);
                        Log.LogInformation("Applying {TweakName}...", selectedTweak.Name);

                        var stopwatch = Stopwatch.StartNew();

                        await selectedTweak.Instance!.Apply(_systemSnapshot).ConfigureAwait(false);

                        stopwatch.Stop();

                        Log.LogDebug("Finished tweak in {Elapsed}ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (Exception e)
                    {
                        Log.LogError(e, "[{Error}]Failed to apply tweak {TweakName}[/], skipped.", Theme.Error,
                            selectedTweak.Name);
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