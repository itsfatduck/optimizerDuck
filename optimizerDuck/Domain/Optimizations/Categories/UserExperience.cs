using Microsoft.Win32;
using optimizerDuck.Common.Helpers;
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

    [Optimization(
        Id = "A3D1E8F2-7B4C-4A5D-9E62-8F3A1B7C4D59",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Visual
    )]
    public sealed class DisableVisualEffects : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "TaskbarAnimations",
                0
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "ListviewShadow",
                0
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency",
                0
            ),
            new(@"HKCU\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Performance), Costs(ImpactArea.Features)],
                Sources =
                [
                    Source(
                        "Tips to improve PC performance in Windows - Microsoft Support",
                        "https://support.microsoft.com/en-us/windows/tips-to-improve-pc-performance-in-windows-b3b3ef5b-5953-fb6a-2528-4bbed82fba96"
                    ),
                ],
                RequiresRestart = RestartRequirement.Explorer,
            };
    }

    [Optimization(
        Id = "950cd979-7483-400b-a207-abd4741a1270",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
            | OptimizationTags.Performance
            | OptimizationTags.Latency
            | OptimizationTags.Privacy
    )]
    public sealed class DisableStartMenuWebSearch : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
            BuildRegistryItems(Shared.IsWindows11OrGreater);

        internal static RegistryItem[] BuildRegistryItems(bool isWindows11OrGreater)
        {
            List<RegistryItem> items = [];

            if (!isWindows11OrGreater)
            {
                items.Add(
                    new(
                        @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                        "BingSearchEnabled",
                        0
                    )
                );
            }

            items.AddRange(
                [
                    new(
                        @"HKCU\Software\Policies\Microsoft\Windows\Explorer",
                        "DisableSearchBoxSuggestions",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                        "DisableSearchBoxSuggestions",
                        1
                    ),
                    new(
                        @"HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                        "DisableWebSearch",
                        1
                    ),
                ]
            );

            return [.. items];
        }

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Latency),
                    Improves(ImpactArea.Privacy),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "DisableSearchBoxSuggestions Explorer Policy - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-admx-windowsexplorer"
                    ),
                ],
                RequiresRestart = RestartRequirement.Explorer,
                SetsGroupPolicy = true,
            };
    }

    [Optimization(
        Id = "3F8B91D2-6A47-4E05-9C2B-E70D14F8A5C6",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.System | OptimizationTags.Network
    )]
    public sealed class MaximizeUpdatePauseLimit : DeclarativeOptimization
    {
        // 3640 days (520 weeks, ~10 years): exact multiple of 7 so the Settings pause dropdown
        // populates cleanly without the date-math overflow / UI hang caused by 0xFFFFFFFF.
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "FlightSettingsMaxPauseDays",
                3640,
                RegistryValueKind.DWord
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Stability), Costs(ImpactArea.Security)],
                Sources =
                [
                    Source(
                        "Manage updates in Windows - Microsoft Support",
                        "https://support.microsoft.com/en-us/windows/manage-updates-in-windows-643e9ea7-3cf6-7da6-a25c-95d4f7f099fe"
                    ),
                ],
            };
    }
}
