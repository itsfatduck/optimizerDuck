using System.Globalization;
using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Optimization;
using optimizerDuck.Services.System;

namespace optimizerDuck.Test.Services;

public class OptimizationValidationTests
{
    [Optimization(
        Id = "11111111-1111-1111-1111-111111111111",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class ValidOptimization : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(Id = "not-a-guid", Risk = OptimizationRisk.Safe, Tags = OptimizationTags.System)]
    private sealed class InvalidIdOptimization : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "22222222-2222-2222-2222-222222222222",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class DuplicateA : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "22222222-2222-2222-2222-222222222222",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
    )]
    private sealed class DuplicateB : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(
            IProgress<ProcessingProgress> progress,
            OptimizationContext context
        )
        {
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Fact]
    public void Validate_ValidId_DoesNotThrow()
    {
        var seen = new HashSet<Guid>();
        var exception = Record.Exception(() =>
            OptimizationValidation.Validate(new ValidOptimization(), seen)
        );

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_InvalidId_ThrowsNamingTheClass()
    {
        var seen = new HashSet<Guid>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OptimizationValidation.Validate(new InvalidIdOptimization(), seen)
        );

        Assert.Contains(nameof(InvalidIdOptimization), ex.Message);
    }

    [Fact]
    public void Validate_DuplicateId_ThrowsNamingTheClass()
    {
        var seen = new HashSet<Guid>();
        OptimizationValidation.Validate(new DuplicateA(), seen);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            OptimizationValidation.Validate(new DuplicateB(), seen)
        );

        Assert.Contains(nameof(DuplicateB), ex.Message);
    }

    [Fact]
    public void Discovery_AllCatalogIds_AreValidAndUnique()
    {
        var seen = new HashSet<Guid>();
        var count = 0;

        foreach (
            var category in ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
        )
        {
            foreach (
                var nested in category
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t =>
                        typeof(IOptimization).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
                    )
            )
            {
                var opt = (BaseOptimization)Activator.CreateInstance(nested)!;
                opt.OwnerType = category;
                OptimizationValidation.Validate(opt, seen);
                count++;
            }
        }

        Assert.True(count > 0, "No optimizations discovered.");
    }

    [Fact]
    public void Discovery_AllCatalogResxKeys_Exist()
    {
        var missing = new List<string>();

        foreach (
            var category in ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
        )
        {
#if DEBUG
            // The Debug category carries hard-coded English, so it has no resource keys to find.
            if (category.Name == "Debug")
                continue;
#endif
            foreach (
                var nested in category
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t =>
                        typeof(IOptimization).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
                    )
            )
            {
                var key = $"Optimizer.{category.Name}.{nested.Name}.Name";
                if (
                    Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
                    is null
                )
                    missing.Add(key);
            }
        }

        Assert.True(missing.Count == 0, "Missing resx keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void Discovery_EveryTagAndEveryStepKind_ExplainsItself()
    {
        var missing = new List<string>();

        // A tag chip looks its explanation up by name, and the record looks up a label per kind of
        // step, so a value added without a key fails here instead of showing a bare key on screen.
        foreach (var tag in Enum.GetValues<OptimizationTags>())
        {
            if (tag == OptimizationTags.None)
                continue;

            var key = $"Optimizer.UI.Tags.{tag}.Tooltip";
            if (Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture) is null)
                missing.Add(key);
        }

        var labelKeys = Enum.GetValues<ChangeKind>()
            .Select(kind => kind.ToDisplay().LabelKey)
            .Append(ChangeKindPresentation.ForStep(new ChangeRecordStep { Ok = false }).LabelKey);

        foreach (var key in labelKeys)
        {
            if (Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture) is null)
                missing.Add(key);
        }

        Assert.True(missing.Count == 0, "Missing resx keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void Discovery_AllMetadataSourcesAndOptionKeys_AreValid()
    {
        var issues = new List<string>();
        static bool HasKey(string? key) =>
            !string.IsNullOrEmpty(key)
            && Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
                is not null;

        foreach (
            var category in ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
        )
        {
#if DEBUG
            if (category.Name == "Debug")
                continue;
#endif
            foreach (
                var nested in category
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t =>
                        typeof(IOptimization).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
                    )
            )
            {
                var opt = (BaseOptimization)Activator.CreateInstance(nested)!;
                opt.OwnerType = category;

                var shortDesc = Translations.ResourceManager.GetString(
                    $"{opt.Prefix}.ShortDescription",
                    CultureInfo.InvariantCulture
                );
                if (string.IsNullOrWhiteSpace(shortDesc))
                    issues.Add($"Missing {opt.Prefix}.ShortDescription");

                var expectsTradeOff =
                    opt.Risk is OptimizationRisk.Moderate or OptimizationRisk.Risky
                    || opt.Metadata.Impacts.Any(static i =>
                        i.Direction == ImpactDirection.TradeOff
                    );
                var tradeOff = Translations.ResourceManager.GetString(
                    $"{opt.Prefix}.TradeOff",
                    CultureInfo.InvariantCulture
                );
                if (expectsTradeOff && string.IsNullOrWhiteSpace(tradeOff))
                    issues.Add($"Missing {opt.Prefix}.TradeOff");

                string[] forbiddenTokens =
                [
                    "=0",
                    "=1",
                    @"HKLM\",
                    @"HKCU\",
                    "DowngradeOnly",
                    "WSAIFabricSvc",
                    "PnPCapabilities",
                    "HiberbootEnabled",
                ];
                foreach (var token in forbiddenTokens)
                {
                    if (
                        shortDesc?.Contains(token, StringComparison.OrdinalIgnoreCase) == true
                        || tradeOff?.Contains(token, StringComparison.OrdinalIgnoreCase) == true
                    )
                        issues.Add(
                            $"Leaked internal token '{token}' in {opt.OptimizationKey} copy"
                        );
                }

                if (opt.Metadata.Impacts.Count == 0)
                    issues.Add($"Missing Metadata.Impacts on {opt.OptimizationKey}");

                if (opt.Metadata.Sources.Count == 0)
                    issues.Add($"Missing Metadata.Sources on {opt.OptimizationKey}");

                foreach (var source in opt.Metadata.Sources)
                {
                    if (
                        string.IsNullOrWhiteSpace(source.Title)
                        || !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri)
                        || uri.Scheme != Uri.UriSchemeHttps
                    )
                        issues.Add($"Invalid source on {opt.OptimizationKey}: '{source.Url}'");
                }

                if (opt is IConfigurableOptimization configurable)
                {
                    foreach (var option in configurable.GetOptions(SystemInfo.Unknown))
                    {
                        if (!HasKey(option.LabelKey))
                            issues.Add($"Missing {option.LabelKey}");
                        if (option.DescriptionKey is not null && !HasKey(option.DescriptionKey))
                            issues.Add($"Missing {option.DescriptionKey}");
                        if (option.TradeOffKey is not null && !HasKey(option.TradeOffKey))
                            issues.Add($"Missing {option.TradeOffKey}");
                        foreach (var choice in option.Choices)
                        {
                            if (!HasKey(choice.LabelKey))
                                issues.Add($"Missing {choice.LabelKey}");
                            if (choice.DescriptionKey is not null && !HasKey(choice.DescriptionKey))
                                issues.Add($"Missing {choice.DescriptionKey}");
                        }
                    }
                }
            }
        }

        Assert.True(issues.Count == 0, string.Join("\n", issues));
    }

    [Fact]
    public void Discovery_AllImpactAndRestartKeys_Exist()
    {
        var missing = new List<string>();

        foreach (var area in Enum.GetValues<ImpactArea>())
        {
            foreach (var direction in Enum.GetValues<ImpactDirection>())
            {
                var key = $"Optimizer.UI.Impact.{area}.{direction}";
                if (
                    Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
                    is null
                )
                    missing.Add(key);
            }
        }

        foreach (var restart in Enum.GetValues<RestartRequirement>())
        {
            if (restart == RestartRequirement.None)
                continue;

            var key = $"Optimizer.UI.Restart.{restart}";
            if (Translations.ResourceManager.GetString(key, CultureInfo.InvariantCulture) is null)
                missing.Add(key);
        }

        Assert.True(missing.Count == 0, "Missing resx keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void Discovery_AllCustomizeResxKeys_Exist()
    {
        var missing = new List<string>();

        foreach (
            var category in ReflectionHelper.FindImplementationsInLoadedAssemblies<ICustomizeCategory>()
        )
        {
            foreach (
                var nested in category
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t =>
                        typeof(ICustomizeSetting).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
                    )
            )
            {
                var prefix = $"Customize.{category.Name}.{nested.Name}";
                if (
                    Translations.ResourceManager.GetString(
                        $"{prefix}.Name",
                        CultureInfo.InvariantCulture
                    )
                    is null
                )
                    missing.Add($"{prefix}.Name");
                if (
                    Translations.ResourceManager.GetString(
                        $"{prefix}.Description",
                        CultureInfo.InvariantCulture
                    )
                    is null
                )
                    missing.Add($"{prefix}.Description");
            }
        }

        Assert.True(
            missing.Count == 0,
            "Missing customize resx keys: " + string.Join(", ", missing)
        );
    }
}
