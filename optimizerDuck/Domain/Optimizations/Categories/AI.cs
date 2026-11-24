using optimizerDuck.Domain.Attributes;
using optimizerDuck.Domain.Conditions;
using optimizerDuck.Domain.Optimizations.Models;
using optimizerDuck.Domain.Optimizations.Models.Services;
using optimizerDuck.Domain.UI;
using optimizerDuck.UI.Pages.Optimize.Categories;

namespace optimizerDuck.Domain.Optimizations.Categories;

[OptimizationCategory(typeof(AIOptimizerPage))]
public class AI() : OptimizationCategoryBase(OptimizationCategoryOrder.AI)
{
    [Optimization(
        Id = "8462E718-42CA-4BE6-940C-3620BF52A533",
        Risk = OptimizationRisk.Moderate,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(RecallInstalledCondition)
    )]
    public class DisableRecall : DeclarativeOptimization
    {
        // DisableAIDataAnalysis stops new snapshots and deletes existing ones;
        // AllowRecallEnablement=0 removes Recall optional bits on Windows 11 24H2+.
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "AllowRecallEnablement", 0),
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots", 1),
            new(@"HKCU\Software\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
            new(@"HKCU\Software\Policies\Microsoft\Windows\WindowsAI", "TurnOffSavingSnapshots", 1),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Storage),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Manage Recall - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/manage-recall"
                    ),
                ],
                RequiresRestart = RestartRequirement.Reboot,
            };
    }

    [Optimization(
        Id = "962C9960-6BFA-4FC5-97D9-BFE772426E39",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11_24H2OrGreaterCondition)
    )]
    public class DisableClickToDo : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1),
            new(@"HKCU\Software\Policies\Microsoft\Windows\WindowsAI", "DisableClickToDo", 1),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Improves(ImpactArea.Clutter)],
                Sources =
                [
                    Source(
                        "Click to Do Policy (DisableClickToDo) - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-windowsai#disableclicktodo"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "64C6BEC3-B58C-4E57-830A-1DE1F4650542",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy
            | OptimizationTags.System
            | OptimizationTags.Windows11Only
            | OptimizationTags.Visual,
        Condition = typeof(Windows11Condition)
    )]
    public class DisableCopilot : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(
                @"HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                "TurnOffWindowsCopilot",
                1
            ),
            new(
                @"HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot",
                "TurnOffWindowsCopilot",
                1
            ),
            new(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                "ShowCopilotButton",
                0
            ),
            new(
                @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked",
                "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}",
                ""
            ),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Privacy), Improves(ImpactArea.Clutter)],
                Sources =
                [
                    Source(
                        "TurnOffWindowsCopilot Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-windowsai#turnoffwindowscopilot"
                    ),
                ],
                RequiresRestart = RestartRequirement.SignOut,
            };
    }

    [Optimization(
        Id = "39097C41-833A-4056-B4B7-C7494E511EF0",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.System
            | OptimizationTags.Ram
            | OptimizationTags.Performance
            | OptimizationTags.Windows11Only,
        Condition = typeof(WsAiFabricServiceExistsCondition)
    )]
    public class DisableAiServiceAutoStart : DeclarativeOptimization
    {
        // Manual rather than Disabled: disabling WSAIFabricSvc has been reported
        // to break taskbar/tray icons on Windows 11 24H2/25H2.
        protected override ServiceItem[] Services { get; } = [ServiceItem.Manual("WSAIFabricSvc")];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts = [Improves(ImpactArea.Memory), Improves(ImpactArea.Privacy)],
                Sources =
                [
                    Source(
                        "Win11Debloat PR #501: Set WSAIFabricSvc to Manual",
                        "https://github.com/Raphire/Win11Debloat/pull/501"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "18B15B8B-E99E-465E-BE7F-E5045B21DEB2",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Ram
    )]
    public class DisableEdgeAI : DeclarativeOptimization
    {
        private const string EdgePolicyPath = @"HKLM\SOFTWARE\Policies\Microsoft\Edge";

        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(EdgePolicyPath, "CopilotCDPPageContext", 0),
            new(EdgePolicyPath, "CopilotPageContext", 0),
            new(EdgePolicyPath, "HubsSidebarEnabled", 0),
            new(EdgePolicyPath, "Microsoft365CopilotChatIconEnabled", 0),
            new(EdgePolicyPath, "EdgeEntraCopilotPageContext", 0),
            new(EdgePolicyPath, "EdgeHistoryAISearchEnabled", 0),
            new(EdgePolicyPath, "ComposeInlineEnabled", 0),
            new(EdgePolicyPath, "GenAILocalFoundationalModelSettings", 1),
            new(EdgePolicyPath, "NewTabPageBingChatEnabled", 0),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Memory),
                    Improves(ImpactArea.Clutter),
                ],
                Sources =
                [
                    Source(
                        "Edge HubsSidebarEnabled Policy - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/hubssidebarenabled"
                    ),
                    Source(
                        "Microsoft Edge Browser Policy Documentation - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "4D9A2F1C-8B3E-4E7A-9C1D-6F2A8B4E5C01",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11Condition)
    )]
    public class DisablePaintAI : DeclarativeOptimization
    {
        private const string PaintPolicyPath =
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint";

        protected override RegistryItem[] RegistryItems { get; } =
        [
            new(PaintPolicyPath, "DisableCocreator", 1),
            new(PaintPolicyPath, "DisableGenerativeFill", 1),
            new(PaintPolicyPath, "DisableImageCreator", 1),
            new(PaintPolicyPath, "DisableGenerativeErase", 1),
            new(PaintPolicyPath, "DisableRemoveBackground", 1),
        ];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Clutter),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "DisableCocreator Paint Policy CSP - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-windowsai#disablecocreator"
                    ),
                ],
            };
    }

    [Optimization(
        Id = "7E3B9A4D-2C6F-4A1E-8D5B-3A9C7E1F4B02",
        Risk = OptimizationRisk.Safe,
        Tags = OptimizationTags.Privacy | OptimizationTags.System | OptimizationTags.Windows11Only,
        Condition = typeof(Windows11Condition)
    )]
    public class DisableNotepadAI : DeclarativeOptimization
    {
        protected override RegistryItem[] RegistryItems { get; } =
        [new(@"HKLM\SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1)];

        public override OptimizationMetadata Metadata { get; } =
            new()
            {
                Impacts =
                [
                    Improves(ImpactArea.Privacy),
                    Improves(ImpactArea.Clutter),
                    Costs(ImpactArea.Features),
                ],
                Sources =
                [
                    Source(
                        "Manage AI features in Notepad - Microsoft Learn",
                        "https://learn.microsoft.com/en-us/windows/client-management/manage-notepad"
                    ),
                ],
            };
    }
}
