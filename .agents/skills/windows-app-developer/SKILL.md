---
name: windows-app-developer
description: Expert in building modern Windows applications using WinUI 3, WPF, and the Windows App SDK. Specializes in MSIX packaging, XAML styling, and MVVM architecture.
---

# Windows App Developer

## Purpose
Provides expertise in building modern Windows desktop applications using WinUI 3, WPF, and Windows App SDK. Specializes in XAML-based UI development, MVVM architecture, native Windows integration, and modern packaging with MSIX.

## When to Use
- Building Windows desktop applications with WinUI 3 or WPF
- Implementing MVVM architecture for Windows apps
- Creating XAML layouts and custom controls
- Packaging applications with MSIX
- Integrating with Windows features (notifications, taskbar, system tray)
- Migrating WPF applications to WinUI 3
- Implementing Windows-specific features (jump lists, live tiles)
- Building Microsoft Store-ready applications

## Quick Start
**Invoke this skill when:**
- Building Windows desktop applications with WinUI 3 or WPF
- Implementing MVVM architecture for Windows apps
- Creating XAML layouts and custom controls
- Packaging applications with MSIX
- Integrating with Windows features (notifications, taskbar)

**Do NOT invoke when:**
- Building cross-platform apps → use mobile-developer or electron-pro
- Console applications → use appropriate language skill
- PowerShell GUI → use powershell-ui-architect
- Web applications → use appropriate web skill

## Decision Framework
```
Windows App Task?
├── New Modern App → WinUI 3 with Windows App SDK
├── Existing WPF App → Maintain or migrate to WinUI 3
├── Cross-Platform Priority → Consider .NET MAUI
├── Enterprise Internal → WPF with proven patterns
├── Store Distribution → MSIX packaging required
└── System Integration → P/Invoke or Windows SDK APIs
```

## Core Workflows

### 1. WinUI 3 Application Setup
1. Create project using Windows App SDK template
2. Configure Package.appxmanifest for capabilities
3. Set up MVVM infrastructure (CommunityToolkit.Mvvm)
4. Implement navigation and shell structure
5. Create reusable control library
6. Configure MSIX packaging
7. Set up CI/CD for Store or sideload distribution

### 2. MVVM Implementation
1. Define ViewModels with observable properties
2. Implement commands for user actions
3. Create services for data and business logic
4. Set up dependency injection container
5. Bind Views to ViewModels in XAML
6. Implement navigation service
7. Add design-time data for XAML preview

### 3. MSIX Packaging
1. Configure Package.appxmanifest
2. Define application identity and capabilities
3. Set up visual assets (icons, splash)
4. Configure installation behavior
5. Sign package with certificate
6. Test installation and updates
7. Submit to Microsoft Store or deploy internally

## Best Practices
- Use WinUI 3 for new development, WPF for legacy maintenance
- Implement MVVM strictly for testability and separation
- Use x:Bind for compile-time binding validation
- Leverage Community Toolkit for common patterns
- Package with MSIX for modern installation experience
- Follow Fluent Design System for consistent UX

## Anti-Patterns
- **Code-behind logic** → Move to ViewModels
- **Synchronous UI operations** → Use async/await for I/O
- **Direct service calls from Views** → Go through ViewModels
- **Ignoring DPI awareness** → Test at multiple scale factors
- **Missing capabilities** → Declare required capabilities in manifest
