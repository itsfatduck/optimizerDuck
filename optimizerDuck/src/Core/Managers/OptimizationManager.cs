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
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace optimizerDuck.Core.Managers;

public class OptimizationManager(SystemSnapshot systemSnapshot)
{
    private static readonly ILogger Log = Logger.CreateLogger<OptimizationManager>();
    private Queue<OptimizationChoice> _selectedOptimizations = new();
    private SystemSnapshot _systemSnapshot = systemSnapshot;

    public static Queue<AppxPackage> SelectedBloatware { get; private set; } =
        new(); // public and static for Remove Bloatware Apps optimization


    public async Task<bool> Begin()
    {

        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);

        Log.LogInformation("Loading optimizations...");

        var optimizationCategories = OptimizationHelper.LoadOptimizationChoices();
        Log.LogInformation("Loaded {CategoriesAmount} categories with {OptimizationAmount} optimizations.", optimizationCategories.Count,
            optimizationCategories.Sum(g => g.Optimizations.Count));

        SystemHelper.Title("Select the optimizations you want to apply");
        try
        {
            using var escapeCancellableConsole = new EscapeCancellableConsole(AnsiConsole.Console);
            var promptOptimizationChoice = new MultiSelectionPrompt<OptimizationChoice>()
                .Title("Select the optimizations you want to apply")
                .InstructionsText(Defaults.EscapeCancellableConsoleMultiSelectionPromptInstructionsText)
                .Required(true)
                .HighlightStyle(Theme.Primary)
                .UseConverter(t => $"{t.Name} {t.Description}")
                .PageSize(24);

            foreach (var g in optimizationCategories)
                promptOptimizationChoice.AddChoiceGroup(
                    new OptimizationChoice(null, $"[bold underline]{g.Name}[/]", string.Empty,
                        false),
                    g.Optimizations
                );

            var optimizationsToSelect = _selectedOptimizations.Count == 0
                                        ? optimizationCategories.SelectMany(g => g.Optimizations).Where(t => t.EnabledByDefault)
                                        : _selectedOptimizations;

            foreach (var optimization in optimizationsToSelect)
            {
                promptOptimizationChoice.Select(optimization);
            }

            var optimizationChoices =
                await escapeCancellableConsole.PromptAsync(promptOptimizationChoice).ConfigureAwait(false);

            _selectedOptimizations = new Queue<OptimizationChoice>(optimizationChoices);

            if (_selectedOptimizations.Any(t => t.Instance is BloatwareAndServices.RemoveBloatwareApps)) // if Bloatware selection is selected
            {
                Log.LogInformation("Getting installed bloatware apps...");

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
                        new PromptOption("Continue", Theme.Success, () => true),
                        new PromptOption("Back", Theme.Warning, () => false)
                    );
                }

                SystemHelper.Title("Select the bloatware apps you want to remove");
                var promptBloatware = new MultiSelectionPrompt<AppxPackage>()
                    .Title("Select the bloatware apps you want to remove")
                    .InstructionsText(Defaults.EscapeCancellableConsoleMultiSelectionPromptInstructionsText)
                    .Required(true)
                    .HighlightStyle(Theme.Primary)
                    .UseConverter(app =>
                        $"{(app.DisplayName.Contains("Safe Apps") || app.DisplayName.Contains("Caution Apps") ?
                            app.DisplayName :
                            $"[bold]{app.DisplayName}[/] [dim]{app.Version} {app.InstallLocation}[/]")}")
                    .PageSize(24);

                if (appxClassification.SafeApps.Count > 0)
                    promptBloatware.AddChoiceGroup(
                        new AppxPackage
                        {
                            DisplayName = $"[bold underline {Theme.Success}]Safe Apps[/] - [{Theme.Success}]Safe to remove[/]",
                            Name = "",
                            Version = "",
                            InstallLocation = ""
                        },
                        appxClassification.SafeApps);

                if (appxClassification.CautionApps.Count > 0)
                    promptBloatware.AddChoiceGroup(
                        new AppxPackage
                        {
                            DisplayName = $"[bold underline {Theme.Error}]Caution Apps[/] - [{Theme.Error}]May cause issues[/]",
                            Name = "",
                            Version = "",
                            InstallLocation = ""
                        },
                        appxClassification.CautionApps);


                if (SelectedBloatware.Count == 0)
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

    public async Task Run(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(Defaults.Logo);
        SystemHelper.Title("Starting Optimization");

        Log.LogInformation("Refreshing system information...");
        _systemSnapshot = await SystemInfoService.RefreshAsync().ConfigureAwait(false);

        Log.LogDebug("Selected {OptimizationAmount} optimizations: {Optimizations}",
                     _selectedOptimizations.Count,
                     string.Join(", ", _selectedOptimizations.Select(t => t.Name)));

        if (_selectedOptimizations.Any(t => t.Instance is BloatwareAndServices.RemoveBloatwareApps)) // if Bloatware selection is selected
            Log.LogDebug("Selected {BloatwareAmount} bloatware apps: {BloatwareApps}", SelectedBloatware.Count,
                string.Join(", ", SelectedBloatware.Select(app => $"{app.DisplayName.Trim()} ({app.Version.Trim()})")));


        Log.LogDebug(new string('=', 60));
        cancellationToken.Register(() =>
        {

        });

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots2)
            .SpinnerStyle($"bold {Theme.Primary}")
            .StartAsyncGlobal("Initializing Optimization", async ctx =>
            {
                while (_selectedOptimizations.TryDequeue(out var selectedOptimization))
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        selectedOptimization = selectedOptimization with
                        {
                            Name = selectedOptimization.Name.Trim(),
                            Description = selectedOptimization.Description.Trim()
                        };

                        SystemHelper.Title(selectedOptimization.Name);
                        ctx.Status($"Applying [{Theme.Primary}]{selectedOptimization.Name}[/]... [{Theme.Muted}][[Press [{Theme.Error}]CTRL + C[/] to cancel the optimization]][/]");

                        using var scope = Log.BeginScope("Optimization: {OptimizationName}", selectedOptimization.Name);


                        Rule($"[bold white] {selectedOptimization.Name} [/]", Theme.Success);
                        Log.LogInformation("Applying {OptimizationName}...", selectedOptimization.Name);

                        var stopwatch = Stopwatch.StartNew();

                        //await selectedOptimization.Instance!.Apply(_systemSnapshot).ConfigureAwait(false);
                        await Task.Delay(5000, cancellationToken);
                        stopwatch.Stop();

                        Log.LogDebug("Applied in {Elapsed} ms", stopwatch.ElapsedMilliseconds);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.LogError(e, "Failed to apply optimization {OptimizationName}. Skipped.", selectedOptimization.Name);
                    }
                    finally
                    {
                        Log.LogDebug(new string('=', 60));
                    }
                }

            });

        if (cancellationToken.IsCancellationRequested)
        {
            SystemHelper.Title("Optimization Canceled");

            Rule("Optimization Canceled", Theme.Error);
            Log.LogInformation("Optimization canceled by user.");
            GlobalStatus.Current?.Status("Optimization canceled by user.");

            PromptDialog.Warning("Optimization Canceled",
                "You have canceled the optimization.",
                new PromptOption("Back", Theme.Warning, () => true));
            return;
        }

        Rule("Optimization completed!", Theme.Success);
        SystemHelper.Title("Optimization completed!");

        Log.LogInformation($"[{Theme.Success}]Optimization completed![/]");

        PromptDialog.Warning("Restart to Apply Changes",
            $"""
            The optimizations have been applied successfully.
            Please [{Theme.Success}]restart[/] your PC for the changes to take effect.
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