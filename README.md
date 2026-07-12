# Nexora RAMFlow

[![Windows Build](https://github.com/krishavi85/Nexora-RAMFlow/actions/workflows/windows-build.yml/badge.svg)](https://github.com/krishavi85/Nexora-RAMFlow/actions/workflows/windows-build.yml)

Nexora RAMFlow is a Windows desktop utility for understanding memory pressure and safely managing SSD-backed virtual memory.

## Important truth

RAMFlow does **not** create physical RAM and does not turn SSD storage into RAM-speed memory. It manages the Windows paging file, explains memory pressure, and helps reduce out-of-memory failures.

## MVP features

- Live physical and virtual-memory dashboard
- Current page-file usage and peak usage
- Top memory-consuming processes
- Balanced, Heavy Applications, Low Storage, and System Managed profiles
- Disk-space-aware recommendations
- Administrator-gated configuration changes
- Automatic JSON backup before every page-file change
- One-click return to Windows system-managed paging
- Windows build and test workflow through GitHub Actions

## Safety model

- Diagnostics run without administrator rights.
- Page-file changes require administrator rights and explicit user confirmation.
- The application never terminates programs automatically.
- Recommendations preserve a disk-space reserve and apply conservative limits.
- Most page-file changes require a Windows restart.

Read [docs/SAFETY.md](docs/SAFETY.md) before enabling write operations.

## Requirements

- Windows 10 or Windows 11, 64-bit
- .NET 8 SDK for development
- Visual Studio 2022 with the .NET desktop development workload, or the `dotnet` CLI

## Build

```powershell
git clone https://github.com/krishavi85/Nexora-RAMFlow.git
cd Nexora-RAMFlow
dotnet restore Nexora.RAMFlow.sln
dotnet build Nexora.RAMFlow.sln --configuration Release
```

## Run

```powershell
dotnet run --project src/Nexora.RAMFlow.App/Nexora.RAMFlow.App.csproj
```

Run normally for diagnostics. Use **Restart as administrator** only when you intend to apply a recommendation.

## Test

```powershell
dotnet test Nexora.RAMFlow.sln --configuration Release
```

## Architecture

```text
src/
├── Nexora.RAMFlow.App/     WPF interface and Windows integration
└── Nexora.RAMFlow.Core/    Platform-independent recommendation engine

tests/
└── Nexora.RAMFlow.Core.Tests/
```

## Current scope

This is the first functional MVP. It is intentionally conservative. Future versions can add historical charts, memory-leak detection, startup analysis, signed installers, localization, and richer workload profiles.

## License

No open-source license has been selected yet. Copyright remains with the repository owner unless a license is added later.
