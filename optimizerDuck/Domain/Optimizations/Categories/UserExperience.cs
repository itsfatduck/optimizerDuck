using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(UserExperienceOptimizerPage))]
public class UserExperience() : OptimizationCategoryBase(OptimizationCategoryOrder.UserExperience)
{
    [Optimization(
        Id = "6B1D8E4A-3C7F-4A2E-9D5B-1E8F3A6C9D07",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance
    )]
    public sealed class DisableExplorerStartupDelay : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                "StartupDelayInMSec",
                0
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                "WaitForIdleState",
                0
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance)],
                Sources =
                [
                    Source(
                        "Startup apps delayed on Windows 11 (StartupDelayInMSec) - Microsoft Q&A",
                        "https://learn.microsoft.com/en-us/answers/questions/4059183/startup-apps-artificially-delayed-on-windows-11"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "2E9F4C7B-8A1D-4E6F-B3C5-9D2A7E4B8F08",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Latency | OptimizationTags.Visual
    )]
    public sealed class ReduceMenuShowDelay : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKCU\Control Panel\Desktop", "MenuShowDelay", "0")];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Latency)],
                Sources =
                [
                    Source(
                        "SystemParameters.MenuShowDelay Property - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/dotnet/api/system.windows.systemparameters.menushowdelay?view=windowsdesktop-10.0"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "31A6274C-AD37-4FFA-A32E-727FB71AD085",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.Visual | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11Condition)
    )]
    public sealed class DisableStartRecommended : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKCU\SOFTWARE\Policies\Microsoft\Windows\Explorer", "HideRecommendedSection", 1)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Clutter), Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "HideRecommendedSection Start Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-start#hiderecommendedsection"
                    ),
                ],
                RequiresRestart = RestartRequirement.Explorer,
            };
    }

    [Optimization(
        Id = "B36652B1-D094-4D39-BEB8-7961E6094A2E",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Visual | OptimizationTags.Privacy
    )]
    public sealed class DisableSettings365Ads : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                "DisableConsumerAccountStateContent",
                1
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Clutter), Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "Experience Policy CSP (Cloud Content) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-experience"
                    ),
                ],
            };
    }
}
