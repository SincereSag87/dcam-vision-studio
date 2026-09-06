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
- Phase 8 - Complete
- Phase 9 - Complete
- Phase 10 - Complete

## Project Status

The planned ten-phase modernization roadmap is complete. DCAM Vision Studio has evolved from a 2024 vendor SDK/sample-based capstone camera-control project into a modern .NET 10 scientific imaging application with hardware abstraction, simulator-backed development, native DCAM adapter architecture, async acquisition, scientific image analysis, in-memory capture history, explicit export, diagnostics, and automated tests.

Phase 1 implemented the camera abstraction, a deterministic simulated camera, basic imaging helpers, unit tests, and a minimal WPF shell wired to the simulator. The simulator allows development and testing without physical camera hardware.

Phase 2 redesigned the WPF application into a polished scientific-camera dashboard. It adds a modern live image workspace, camera/device panel, camera settings summary, frame statistics, histogram visualization, auto contrast for preview display, improved status feedback, and simulator-backed operation.

Phase 3 adds a complete exposure control workspace for scientific imaging. It includes validated exposure presets, unit conversion across microseconds, milliseconds, and seconds, camera-reported exposure ranges, logarithmic slider adjustment, exposure sweep automation, cancellation, result browsing, exposure-response visualization, and saturation analysis.

Phase 4 replaces the basic live loop with a robust producer/consumer live acquisition pipeline. It uses a bounded frame buffer, configurable overflow behavior, rolling acquisition/display FPS metrics, dropped-frame and source-gap tracking, preview throttling, latest-frame display semantics, pause/resume preview behavior, clean shutdown, and high-rate simulator testing. Pause Preview keeps acquisition running while presentation is paused; Resume Preview displays the newest available processed frame.

Phase 5 adds a scientific image display and histogram-processing workspace. It preserves raw 16-bit camera data while applying display-only transforms for manual black/white LUT range, auto min/max contrast, percentile contrast, gamma, inversion, threshold visualization, advanced histogram statistics, histogram linear/log rendering, full/display histogram ranges, clipping analysis, display presets, and lightweight processing-time feedback.

Phase 6 adds a metadata-driven dynamic camera property explorer. Camera properties now include stable IDs, categories, descriptions, data types, ranges, units, enum options, access metadata, streaming-write rules, and availability state. The WPF explorer supports search, category filtering, writable-only filtering, metadata-driven editors, Apply/Revert, friendly validation errors, simulator dependency rules, and synchronization with exposure workflows. The model is designed so a future DCAM adapter can map real Hamamatsu property metadata without hardcoded WPF controls.

Phase 7 adds in-memory capture history and session management. Manual captures are retained automatically, exposure-sweep frames are retained at sweep completion, and live frames can be saved deliberately with Save Current. Each record owns a cloned frame buffer, immutable capture-time metadata, camera property snapshots, statistics, notes, and tags. History supports bounded retention, memory estimates, session creation/rename, selected historical frame viewing, local search/filter/sort, notes/tags, deletion, clear confirmation, and two-capture comparison metrics. Capture history is currently in-memory only.

Phase 8 adds explicit image and metadata export from capture history. It supports single-capture, filtered batch, and session exports with 16-bit scientific TIFF, 8-bit PNG previews, optional raw Mono16 files, JSON metadata sidecars, filename templates, Windows-safe collision handling, by-session directory layout, progress/cancellation, atomic file writes, export manifests, checksums, and approximate export-size estimates. Scientific TIFF and raw exports preserve the original 16-bit pixel data; display processing applies only to preview PNG files. Capture history remains in-memory unless the user explicitly exports selected captures.

Phase 9 adds the native Hamamatsu DCAM adapter behind the existing `ICameraService` architecture. The application now supports Auto, Simulator, and Hamamatsu DCAM camera sources, checks whether `dcamapi.dll` can be loaded, initializes/uninitializes the DCAM API through an isolated runtime service, enumerates hardware devices when available, maps native properties into the metadata-driven property explorer, converts exposure values between DCAM seconds and application `TimeSpan`, captures/streams copied Mono16/Mono8 frames, and keeps simulator fallback available when the runtime or hardware is missing. Hardware-specific tests are isolated from normal unit tests.

Hamamatsu DCAM runtime and driver binaries are external dependencies and are not distributed with this repository. Install the supported Hamamatsu DCAM-API runtime and camera driver separately, use an x64 process/runtime combination, then select Auto or Hamamatsu DCAM in the application. If the runtime is unavailable or no hardware is attached, DCAM Vision Studio still launches and remains usable with the simulator.

Phase 10 adds professional diagnostics and structured logging. The application writes structured rolling log files to `%LOCALAPPDATA%\DCAMVisionStudio\Logs\`, keeps a bounded in-memory log/error view for the diagnostics workspace, exposes application/runtime/camera/DCAM/acquisition/imaging/history/export diagnostics, applies simple health rules, handles unhandled exceptions through the logging system, and can generate privacy-conscious support bundles with diagnostics JSON, text reports, recent errors, recent logs, and a manifest. Native DCAM integration is implemented and covered by mocked/native-boundary tests. Physical hardware validation remains environment-dependent.

## Architecture

- `DcamVision.App` - WPF desktop application and MVVM shell.
- `DcamVision.Core` - interfaces, domain models, camera abstractions, capture settings, property metadata, validation, and simulated camera service.
- `DcamVision.Dcam` - native Hamamatsu DCAM adapter layer with isolated interop, runtime detection, property mapping, capture, and streaming services.
- `DcamVision.Imaging` - frame conversion, LUT/display processing, histogram analysis, acquisition pipeline utilities, in-memory capture history, and image/metadata export.
- `DcamVision.Tests` - xUnit coverage for simulator and imaging behavior.

## Final Architecture

```text
WPF / MVVM
      |
      v
Camera Abstraction
  |          |
Simulator   DCAM
      |
      v
Acquisition Pipeline
      |
      v
Imaging / Analysis
      |
      v
History
      |
      v
Export

Cross-cutting:
Logging / Diagnostics
```

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

Use the Camera Source selector to choose:

- Auto - discover DCAM hardware when available and always keep the simulator available.
- Simulator - use only the built-in simulated Hamamatsu camera.
- Hamamatsu DCAM - use only real DCAM hardware when the runtime and camera are available.

The DCAM adapter expects an x64 Hamamatsu DCAM runtime. Proprietary files such as `dcamapi.dll` should come from the normal vendor installation and should not be committed to this repository.

## Diagnostics

Runtime logs are written outside the repository:

```text
%LOCALAPPDATA%\DCAMVisionStudio\Logs\
```

Logs roll daily using names such as `dcam-vision-studio-2026-09-05.log` and are retained for a bounded period. The Diagnostics workspace shows health status, runtime/backend diagnostics, live acquisition metrics, current image-processing state, capture-history/export summaries, recent warnings/errors, and a bounded in-memory log viewer with level/category/search filtering.

Support bundles are generated under:

```text
%LOCALAPPDATA%\DCAMVisionStudio\SupportBundles\
```

Bundles include diagnostics JSON, a text report, recent errors, selected recent log files, a manifest, and a README. Image captures, exported scientific data, usernames, full personal paths, and camera serial numbers are not included by default. Camera serial inclusion is controlled by support-bundle options.

## Test

```powershell
dotnet test
```

Normal tests use simulator and mock-native DCAM coverage only. Optional hardware checks are marked with the `Hardware` trait and are skipped by default; run them only on a workstation with the Hamamatsu runtime and supported camera attached.

## Roadmap

- DONE Phase 1 - Camera abstraction and simulated camera
- DONE Phase 2 - Modern camera dashboard
- DONE Phase 3 - Exposure controls and presets
- DONE Phase 4 - Live acquisition pipeline
- DONE Phase 5 - LUT and Advanced Histogram Processing
- DONE Phase 6 - Dynamic camera property explorer
- DONE Phase 7 - Capture history
- DONE Phase 8 - Image and metadata export
- DONE Phase 9 - Native Hamamatsu DCAM adapter
- DONE Phase 10 - Diagnostics and logging
