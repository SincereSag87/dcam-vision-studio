# DCAM Vision Studio

DCAM Vision Studio is a modern scientific-camera control application built with C#, .NET 10, and WPF. It is a clean rebuild of a 2024 college capstone project that originally involved a real external client and Hamamatsu DCAM SDK integration.

The original project used Hamamatsu DCAM SDK sample code as a starting point. This repository is being rebuilt from scratch with a production-style .NET architecture. Vendor SDK code is not copied into this repository; the old SDK material is only future reference for the native camera integration layer.

## Phase 1 Status

Phase 1 implements the camera abstraction, a deterministic simulated camera, basic imaging helpers, unit tests, and a minimal WPF shell wired to the simulator. The simulator allows development and testing without physical camera hardware.

Hamamatsu DCAM integration is not complete yet. `DcamVision.Dcam` exists as the dedicated future adapter layer and intentionally contains only a placeholder implementation.

## Architecture

- `DcamVision.App` - WPF desktop application and MVVM shell.
- `DcamVision.Core` - interfaces, domain models, camera abstractions, capture settings, and simulated camera service.
- `DcamVision.Dcam` - future Hamamatsu DCAM adapter layer.
- `DcamVision.Imaging` - frame conversion, LUT, and histogram utilities.
- `DcamVision.Tests` - xUnit coverage for simulator and imaging behavior.

## Build

```powershell
dotnet restore
dotnet build
```

## Run

```powershell
dotnet run --project .\DcamVision.App\DcamVision.App.csproj
```

The Phase 1 workflow is:

1. Discover
2. Select the simulated camera
3. Connect
4. Capture
5. Start Live
6. Stop Live
7. Change exposure
8. Disconnect

## Test

```powershell
dotnet test
```

## Roadmap

- TODO Phase 2 - Modern camera dashboard
- TODO Phase 3 - Exposure controls and presets
- TODO Phase 4 - Live acquisition pipeline
- TODO Phase 5 - LUT and histogram image processing
- TODO Phase 6 - Dynamic camera property explorer
- TODO Phase 7 - Capture history
- TODO Phase 8 - Image and metadata export
- TODO Phase 9 - Hamamatsu DCAM adapter
- TODO Phase 10 - Diagnostics and logging
