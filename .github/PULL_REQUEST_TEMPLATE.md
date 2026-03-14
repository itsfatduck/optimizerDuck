## Description
<!-- 
Provide a detailed description of the changes applied. 
If this PR is adding a new Optimization, briefly explain how it works and what it modifies in the Windows OS.
Link any relevant GitHub issues here (e.g., "Fixes #123" or "Closes #456").
-->

## Type of Change
<!-- Please check the options that apply -->
- [ ] 🐛 Bug fix (`fix:` - non-breaking change which fixes an issue)
- [ ] ⚡ New Optimization/Feature (`feat:` - adds a new optimization or feature)
- [ ] ✨ New App Feature (`feat:` - adds UI, services, or core app functionality)
- [ ] 🛠️ Refactoring (`refactor:` - code restructure that neither fixes a bug nor adds a feature)
- [ ] 📝 Documentation update (`docs:`)
- [ ] 🌐 Translation/Localization update (`i18n:` or `chore:`)

## Checklist
<!-- Before hitting that submit button on GitHub, check these boxes: -->

- [ ] Code builds without errors or warnings (`dotnet build`).
- [ ] All unit tests pass locally (`dotnet test`).
- [ ] Code perfectly maps to our naming and style conventions.
- [ ] UI changes include a screenshot/GIF in the Screenshots or Video section.

### For New Optimizations (Skip if irrelevant)
- [ ] I inherited from `BaseOptimization`.
- [ ] I included the `[Optimization]` attribute with a valid `Guid` formatted `Id`, a correct `OptimizationRisk`, and appropriate `Tags`.
- [ ] All strings added are placed in `.resx` files; no hardcoded strings.
- [ ] Revert logic for new optimizations uses `RegistryService/ShellService` or manually records `RevertManager.Record()`.

## Screenshots or Video (if applicable)
<!-- 
If your pull request modifies or creates UI elements, please include a visual screenshot or GIF here. 
If it's purely a backend or script logic change, you can delete this section.
-->

## Additional Context
<!-- 
Add any other context about the pull request here. Are there any side effects? Any registry paths that reviewers should manually verify?
-->
