using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using optimizerDuck.Core.Interfaces;
using optimizerDuck.Core.Models.Attributes;
using optimizerDuck.Core.Models.Optimization;
using optimizerDuck.Core.Models.Optimization.Services;
using optimizerDuck.Core.Models.UI;
using optimizerDuck.Services.Managers;
using optimizerDuck.Services.OptimizationServices;
using optimizerDuck.UI.Views.Pages.Optimizations;

namespace optimizerDuck.Core.Optimizers;

[OptimizationCategory(typeof(UserExperienceOptimizerPage))]
public class UserExperience : IOptimizationCategory
{
    public string Name { get; init; } = Loc.Instance[$"Optimizer.{nameof(UserExperience)}"];
    public OptimizationCategoryOrder Order { get; init; } = OptimizationCategoryOrder.UserExperience;
    public ObservableCollection<IOptimization> Optimizations { get; init; } = [];

    [Optimization(
        Id = "B4E0B6C1-7A1C-4A92-8D38-9B8E5F2D8D11",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual | OptimizationTags.Privacy)]
    public sealed class DisableTaskbarNewsAndInterests : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKLM\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                new RegistryItem(@"HKCU\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Feeds",
                    "ShellFeedsTaskbarViewMode", 2)
            );
            context.Logger.LogInformation("Disabled taskbar news and interests");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "2F8E9B9A-31D5-4B4D-BE47-6F3E9C90A7C3",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual)]
    public sealed class DisableTaskbarExtraButtons : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarDa", 0), // Widgets
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarMn", 0), // Chat
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ShowTaskViewButton", 0)
            );
            context.Logger.LogInformation("Disabled extra taskbar buttons");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "8C9F43B4-5C5D-4F71-9E4E-1C7E9B4E6D90",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual | OptimizationTags.Privacy)]
    public sealed class DisableTaskbarSearchAndBing : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                    "SearchboxTaskbarMode", 0),
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Search",
                    "BingSearchEnabled", 0)
            );
            context.Logger.LogInformation("Disabled taskbar search and Bing");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "4A1F6B78-4E5E-4A77-A5C9-7C6F3D2E1B90",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System)]
    public sealed class RemoveMeetNowAndPeople : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer",
                    "HideSCAMeetNow", 1),
                new RegistryItem(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People",
                    "PeopleBand", 0)
            );
            context.Logger.LogInformation("Removed Meet Now and People icons");
            return Task.FromResult(ApplyResult.True());
        }
    }



    [Optimization(
        Id = "E1C2D3A4-B5F6-47A8-9C0D-1F2E3A4B5C6D",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System)]
    public sealed class EnableTaskbarEndTask : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings",
                    "TaskbarEndTask", 1)
            );
            context.Logger.LogInformation("Enabled taskbar end task feature");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "C08A5C23-6977-4C14-8CB2-F398CD85B3EE",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual | OptimizationTags.Power)]
    public sealed class EnableDarkMode : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 0),
                new RegistryItem(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "SystemUsesLightTheme", 0)
            );
            context.Logger.LogInformation("Enabled dark mode");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "5A6E2C9D-3F7E-4B92-A9D1-7F3C2A6E8813",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Privacy)]
    public sealed class DisableExplorerAndSystemNotifications : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ShowSyncProviderNotifications", 0),
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                    "SystemPaneSuggestionsEnabled", 0),
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "ToastEnabled", 0),
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications",
                    "LockScreenToastEnabled", 0)
            );
            context.Logger.LogInformation("Disabled Explorer and system notifications");
            return Task.FromResult(ApplyResult.True());
        }
    }


    [Optimization(
        Id = "B9D2F8A7-1A52-4E73-8E19-92C3A6E7D0C2",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual)]
    public sealed class ShowFileExtensionsAndHiddenFiles : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "HideFileExt", 0)
            );
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "Hidden", 1)
            );
            context.Logger.LogInformation("Showed file extensions and hidden files");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "1A4D9E7F-9B73-4A0E-B5C2-7D9F1E6C4A55",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Visual)]
    public sealed class EnableClassicContextMenu : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                    "")
            );
            context.Logger.LogInformation("Enabled classic context menu");
            return Task.FromResult(ApplyResult.True());
        }
    }


    [Optimization(
        Id = "9E2C4F7D-5A6B-4E9D-8C71-6E2F5A9C3314",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Performance | OptimizationTags.Latency)]
    public sealed class SpeedUpExplorerAndMenus : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                    "StartupDelayInMSec", 0),
                new(@"HKCU\Control Panel\Desktop",
                    "MenuShowDelay", "0")
            );
            context.Logger.LogInformation("Speeded up Explorer and menus");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "C7A98B41-0F1E-4E0D-8D4F-5A7F2E3D9B66",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Visual)]
    public sealed class ShowSecondsInSystemClock : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new RegistryItem(
                    @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ShowSecondsInSystemClock", 1)
            );
            context.Logger.LogInformation("Showed seconds in system clock");
            return Task.FromResult(ApplyResult.True());
        }
    }

    [Optimization(
        Id = "7D9A4C3E-6F1B-4E8C-A9F2-3D7E5C6A8815",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System | OptimizationTags.Visual | OptimizationTags.Performance)]
    public sealed class DisableVisualEffects : BaseOptimization
    {
        public override Task<ApplyResult> ApplyAsync(IProgress<ProcessingProgress> progress, OptimizationContext context)
        {
            RegistryService.Write(
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "TaskbarAnimations", 0),
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                    "ListviewShadow", 0),
                new(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "EnableTransparency", 0),
                new(@"HKCU\Software\Microsoft\Windows\DWM",
                    "EnableAeroPeek", 0)
            );
            context.Logger.LogInformation("Disabled visual effects");
            return Task.FromResult(ApplyResult.True());
        }
    }
}