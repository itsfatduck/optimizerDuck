---
name: "⚡ Optimization Tweak Request"
about: Request a new optimization or tweak to be added to optimizerDuck
title: 'feat: add [optimization name]'
labels: 'enhancement, optimization'
assignees: ''
---

**Optimization Name**
<!-- A brief, clear name for the optimization (e.g., `Disable Telemetry`, `Enable Game Mode`). -->

**Short Description**
<!-- A one-sentence description of the optimization (e.g., `Disables Microsoft telemetry services to improve privacy`). -->

**Technical Details (Implementation Code)**
Provide the exact Registry Key, PowerShell command, CMD script, or C# code required to apply this optimization.
```text
Registry Key: 
Value Name: 
Value Data: 
Type: (DWORD/QWORD/String)
--- or ---
Script/Code:

```

**Impact and Effect**
<!-- Explain what this optimization actually does under the hood. Why would a user want to apply it? What is improved? -->

**Category & Location**
<!-- Where should this optimization be located in the app? (e.g., Privacy, System, Gaming, Network, Context Menu). -->

**Risk Level**
Please suggest a risk level for this optimization based on our `OptimizationRisk` enum:
- [ ] Safe (Easily reversible, zero system impact)
- [ ] Moderate (May disable certain obscure features or affect some apps)
- [ ] Risky (Core system/registry change, could cause instability if used incorrectly)

**Tags**
<!-- List the relevant `OptimizationTags` (e.g., Privacy, Performance, System, UI, Network, Storage, etc.) -->

**References & Documentation**
<!-- Add any links to Microsoft documentation, GitHub issues, or technical articles that validate this tweak. -->
