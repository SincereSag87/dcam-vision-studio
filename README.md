# DCAM Vision Studio

DCAM Vision Studio is a modern scientific-camera control application built with C#, .NET 10, and WPF. It is a clean rebuild of a 2024 college capstone project that originally involved a real external client and Hamamatsu DCAM SDK integration.

The original project used Hamamatsu DCAM SDK sample code as a starting point. This repository is being rebuilt from scratch with a production-style .NET architecture. Vendor SDK code is not copied into this repository; the old SDK material is only future reference for the native camera integration layer.

## Status

- Phase 1 - Complete
- Phase 2 - Complete
- Phase 3 - Complete
- Phase 4 - Complete
- Phase 5 - Complete
- Phase 6 - Complete
- Phase 7 - Complete

Phase 1 implemented the camera abstraction, a deterministic simulated camera, basic imaging helpers, unit tests, and a minimal WPF shell wired to the simulator. The simulator allows development and testing without physical camera hardware.

Phase 2 redesigned the WPF application into a polished scientific-camera dashboard. It adds a modern live image workspace, camera/device panel, camera settings summary, frame statistics, histogram visualization, auto contrast for preview display, improved status feedback, and simulator-backed operation.

Phase 3 adds a complete exposure control workspace for scientific imaging. It includes validated exposure presets, unit conversion across microseconds, milliseconds, and seconds, camera-reported exposure ranges, logarithmic slider adjustment, exposure sweep automation, cancellation, result browsing, exposure-response visualization, and saturation analysis.

Phase 4 replaces the basic live loop with a robust producer/consumer live acquisition pipeline. It uses a bounded frame buffer, configurable overflow behavior, rolling acquisition/display FPS metrics, dropped-frame and source-gap tracking, preview throttling, latest-frame display semantics, pause/resume preview behavior, clean shutdown, and high-rate simulator testing. Pause Preview keeps acquisition running while presentation is paused; Resume Preview displays the newest available processed frame.

Phase 5 adds a scientific image display and histogram-processing workspace. It preserves raw 16-bit camera data while applying display-only transforms for manual black/white LUT range, auto min/max contrast, percentile contrast, gamma, inversion, threshold visualization, advanced histogram statistics, histogram linear/log rendering, full/display histogram ranges, clipping analysis, display presets, and lightweight processing-time feedback.

Phase 6 adds a metadata-driven dynamic camera property explorer. Camera properties now include stable IDs, categories, descriptions, data types, ranges, units, enum options, access metadata, streaming-write rules, and availability state. The WPF explorer supports search, category filtering, writable-only filtering, metadata-driven editors, Apply/Revert, friendly validation errors, simulator dependency rules, and synchronization with exposure workflows. The model is designed so a future DCAM adapter can map real Hamamatsu property metadata without hardcoded WPF controls.

Phase 7 adds in-memory capture history and session management. Manual captures are retained automatically, exposure-sweep frames are retained at sweep completion, and live frames can be saved deliberately with Save Current. Each record owns a cloned frame buffer, immutable capture-time metadata, camera property snapshots, statistics, notes, and tags. History supports bounded retention, memory estimates, session creation/rename, selected historical frame viewing, local search/filter/sort, notes/tags, deletion, clear confirmation, and two-capture comparison metrics. Capture history is currently in-memory only.

Hamamatsu DCAM integration is not complete yet. `DcamVision.Dcam` exists as the dedicated future adapter layer and intentionally contains only a placeholder implementation.

## Architecture

- `DcamVision.App` - WPF desktop application and MVVM shell.
- `DcamVision.Core` - interfaces, domain models, camera abstractions, capture settings, property metadata, validation, and simulated camera service.
- `DcamVision.Dcam` - future Hamamatsu DCAM adapter layer.
- `DcamVision.Imaging` - frame conversion, LUT/display processing, histogram analysis, acquisition pipeline utilities, and in-memory capture history.
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

The simulator-backed workflow is:

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

- DONE Phase 1 - Camera abstraction and simulated camera
- DONE Phase 2 - Modern camera dashboard
- DONE Phase 3 - Exposure controls and presets
- DONE Phase 4 - Live acquisition pipeline
- DONE Phase 5 - LUT and Advanced Histogram Processing
- DONE Phase 6 - Dynamic camera property explorer
- DONE Phase 7 - Capture history
- TODO Phase 8 - Image and metadata export
- TODO Phase 9 - Hamamatsu DCAM adapter
- TODO Phase 10 - Diagnostics and logging
