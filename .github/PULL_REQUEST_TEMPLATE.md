## 📝 Description
<!-- 
Provide a detailed description of the changes applied. 
If this PR is adding a new Optimization, briefly explain how it works and what it modifies in the Windows OS.
Link any relevant GitHub issues here (e.g., "Fixes #123" or "Closes #456").
-->

## 🔄 Type of Change
<!-- Please check the options that apply -->
- [ ] 🐛 Bug fix (`fix:` - non-breaking change which fixes an issue)
- [ ] ⚡ New Optimization/Tweak (`feat:` - adds a new system tweak or optimization rule)
- [ ] ✨ New App Feature (`feat:` - adds UI, services, or core app functionality)
- [ ] 🛠️ Refactoring (`refactor:` - code restructure that neither fixes a bug nor adds a feature)
- [ ] 📝 Documentation update (`docs:`)
- [ ] 🌐 Translation/Localization update (`chore:` or `feat:`)

## ✅ Checklist
<!-- Please ensure all of the following points are checked before submitting the PR -->
- [ ] **Commit Messages:** My PR title and commit messages follow the **Conventional Commits** format (e.g., `feat: add game mode optimization`, `fix: correct layout glitch`).
- [ ] **Compilation & Tests:** I have built the application (`dotnet build optimizerDuck.slnx`) and all tests pass (`dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj`).
- [ ] **Code Style:** My C# code follows the project's naming conventions (`PascalCase` for classes/methods, `camelCase` for vars) and styling rules.
- [ ] **Localization (If adding UI text):** I did NOT hardcode UI text. I have updated the `.resx` files located in the `optimizerDuck/Resources/Languages/` directory.

### For New Optimizations (Skip if irrelevant)
- [ ] I inherited from `BaseOptimization` (or implemented `IOptimization`).
- [ ] I included the `[Optimization]` attribute with a valid `Guid` formatted `Id`, a correct `OptimizationRisk`, and appropriate `Tags`.
- [ ] I mapped the string keys properly so the Name and Description can be translated.

## 📸 Screenshots or Video (if applicable)
<!-- 
If your pull request modifies or creates UI elements, please include a visual screenshot or GIF here. 
If it's purely a backend or script logic change, you can delete this section.
-->

## ⚠️ Additional Context
<!-- 
Add any other context about the pull request here. Are there any side effects? Any registry paths that reviewers should manually verify?
-->
