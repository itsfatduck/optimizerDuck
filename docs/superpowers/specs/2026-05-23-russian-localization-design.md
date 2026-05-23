# Design Specification: Russian Language Support (ru-RU)

## Goal
Implement full Russian language (`ru-RU`) support in the `optimizerDuck` application and translate all user-facing strings naturally.

## User Review Required
No breaking changes are introduced. The core terminology chosen by the user:
- `Dashboard` -> "Главная" / "Панель управления"
- `Revert` -> "Откатить"
- `Bloatware` -> "Встроенное ПО / Мусорные приложения"

## Proposed Changes

### 1. Localization Resource File
- **Target File**: [Translations.ru-RU.resx](file:///c:/Users/anton/.gemini/antigravity-ide/scratch/optimizerDuck/optimizerDuck/Resources/Languages/Translations.ru-RU.resx)
- **Description**: A new `.resx` localization file based on `Translations.resx`, containing translations of all 537 entries. It will be generated using a translation script with Google Translate API, followed by adjustments for core terms.

### 2. Settings ViewModel Update
- **Target File**: [SettingsViewModel.cs](file:///c:/Users/anton/.gemini/antigravity-ide/scratch/optimizerDuck/optimizerDuck/UI/ViewModels/Pages/SettingsViewModel.cs)
- **Description**: Add Russian (`ru-RU`) to the `Languages` observable collection:
  ```csharp
  new() { DisplayName = "Русский", Culture = new CultureInfo("ru-RU") }
  ```

## Translation Strategy
A dependency-free Python script will:
1. Parse the XML structure of `Translations.resx`.
2. Send each `<value>` to the Google Translate API.
3. Keep placeholders like `{0}` intact.
4. Replace core terminology with chosen terms:
   - Dashboard -> Главная (for sidebar/navigation) or Панель управления (for other titles)
   - Revert -> Откатить
   - Bloatware -> Встроенное ПО / Мусорные приложения
5. Write the resulting XML to `Translations.ru-RU.resx`.

## Verification Plan

### Automated Tests
- Run `dotnet build optimizerDuck.slnx --configuration Release` to verify compilation.
- Run `dotnet test optimizerDuck.Test/optimizerDuck.Test.csproj --configuration Release --no-build` to ensure all tests pass.

### Manual Verification
- Launch the application, navigate to Settings, switch language to "Русский", and verify that UI elements update correctly.
