# NeoTaskmgr – Implementation Plan

A modern C# / WinUI 3 re-implementation of Windows Task Manager.

## 1. Scope

| Task Manager page | In scope | Notes |
|---|---|---|
| Processes | Yes | Grouped view (Apps / Background / Windows processes), heat-map columns, end task, efficiency mode |
| Performance | Yes | CPU, Memory, Disk(s), Ethernet/Wi‑Fi, GPU(s) with live graphs |
| App history | **No** | Out of scope |
| Startup apps | Yes | Registry Run keys + Startup folders, enable/disable |
| Users | **No** | Out of scope |
| Details | Yes | Flat process list, column picker, priority/affinity, dump file |
| Services | Yes | List, start/stop/restart, go to process |
| Settings | Yes | Start page, update speed, always on top, theme |

Explicitly not planned for v1: "Startup impact" (Windows computes this from internal diagnostics data), the "Users" view's session management, NPU graphs (can be added later on the same pattern as GPU).

## 2. Guiding principles

- **Simple and clean**: small, focused classes; no frameworks where 50 lines of code suffice.
- **Use the platform**: .NET BCL + WinUI controls first. Win32 APIs via CsWin32-generated P/Invoke where .NET has no equivalent.
- **No extra NuGet packages without approval** (see §8 for the approved list).
- **UI never blocks**: all data collection on a background thread; UI only applies snapshots.

## 3. Technology & project setup

- **.NET 10** (`net10.0-windows10.0.22621.0`, `SupportedOSPlatformVersion` 10.0.22000 = **Windows 11 only**), C# latest, `Nullable` enabled, `TreatWarningsAsErrors`.
- **WinUI 3 / Windows App SDK** (latest stable).
- **Unpackaged, self-contained Windows App SDK** (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`).
  Reason: Task Manager needs to run elevated (`requestedExecutionLevel = highestAvailable` in `app.manifest`) to see/manage all processes and services. Elevation is straightforward for unpackaged apps and awkward for MSIX.
- Settings persisted as JSON (`System.Text.Json`) in `%LocalAppData%\NeoTaskmgr\settings.json` (`ApplicationData` is not available unpackaged).

### Solution layout

```
NeoTaskmgr.slnx
Directory.Build.props          # shared TFM, nullable, analyzers, warnings-as-errors
.editorconfig
src/
  NeoTaskmgr/                  # WinUI app (Views, ViewModels, Controls, App shell)
    App.xaml, MainWindow.xaml
    Controls/                  # PerformanceGraph, DataTable, HeatCell ...
    Pages/                     # ProcessesPage, PerformancePage, StartupPage, DetailsPage, ServicesPage, SettingsPage
    ViewModels/
    Mvvm/                      # ObservableObject, RelayCommand (tiny, hand-written)
  NeoTaskmgr.Core/             # No UI dependency – all data collection & actions
    NativeMethods.txt          # CsWin32: list of Win32 APIs to generate (PInvoke.* in namespace Windows.Win32)
    NativeMethods.json         # CsWin32 options (e.g. allowMarshaling: false, public: false)
    Interop/                   # Only the few signatures CsWin32 metadata lacks (e.g. undocumented ntdll calls) + safe wrappers
    Sampling/                  # SamplingService, snapshots
    Processes/                 # ProcessProvider, ProcessGrouper, ProcessActions
    Network/                   # EtwNetworkMonitor (TraceEvent) – per-process network bytes
    Performance/               # CpuProvider, MemoryProvider, DiskProvider, NetworkProvider, GpuProvider
    Services/                  # ServiceProvider, ServiceActions
    Startup/                   # StartupProvider
    Formatting/                # Byte/percent/rate formatting
tests/
  NeoTaskmgr.Core.Tests/       # xUnit tests for pure logic (rate calculations, grouping, StartupApproved parsing, SMBIOS parsing, formatting)
```

Splitting Core from the UI keeps the Win32 code testable and the XAML project thin.

## 4. Architecture

```
 ┌──────────────┐  Tick (interval from settings)   ┌─────────────────────┐
 │SamplingService│ ───────────────────────────────▶ │ Providers (Core)    │
 │ PeriodicTimer │ ◀─────── immutable snapshots ─── │ Process/CPU/Mem/... │
 └──────┬───────┘                                   └─────────────────────┘
        │ SnapshotReady event (background thread)
        ▼
 ┌──────────────┐ DispatcherQueue.TryEnqueue ┌──────────────┐  x:Bind  ┌──────┐
 │ ViewModels   │ ─────────────────────────▶ │ Observable   │ ───────▶ │ Pages│
 │ (per page)   │   diff/merge by key (PID)  │ collections  │          └──────┘
 └──────────────┘                            └──────────────┘
```

- **SamplingService**: one `PeriodicTimer` loop. Interval from settings: High 0.5 s / Normal 1 s / Low 4 s / Paused. Each tick calls the providers that the *visible* page needs (plus CPU/memory, which are always cheap and feed the graph history).
- **Providers** return immutable `record` snapshots. Rates (CPU %, disk/network bytes/s) are computed from deltas between two samples – pure functions, unit-tested.
- **ViewModels** merge snapshots into existing row view-models keyed by PID / service name, updating properties in place (so selection and scroll position survive refreshes), adding/removing rows as needed.
- **MVVM**: hand-written `ObservableObject` (INotifyPropertyChanged + `SetProperty`) and `RelayCommand`. `x:Bind` everywhere (compiled bindings, no reflection).
- **No DI container**: `App` composes the few services manually.

## 5. Data sources per feature (BCL first, Win32 when needed)

### Processes / Details
| Data | Source |
|---|---|
| Process list, CPU time, working set, private bytes, handles, threads, session | `NtQuerySystemInformation(SystemProcessInformation)` – one call returns everything for all processes (much cheaper than `Process.GetProcesses()` + per-process queries) |
| Name, description, publisher, icon | `FileVersionInfo`, `Icon`-less: `SHGetFileInfo` → `SoftwareBitmapSource` (cached per image path) |
| Image path, command line | `QueryFullProcessImageName`, `NtQueryInformationProcess(ProcessCommandLineInformation)` |
| User name | `OpenProcessToken` + `GetTokenInformation` → `SecurityIdentifier.Translate` (BCL) |
| Disk activity per process | `GetProcessIoCounters` delta (approximation – includes non-disk I/O; Task Manager uses ETW) |
| GPU per process | PDH counters `\GPU Engine(*)\Utilization Percentage` (instance names contain `pid_N`) |
| Network per process | ETW kernel TCP/IP + UDP events via **TraceEvent** (`EtwNetworkMonitor`): a real-time session runs on a background thread for the app's lifetime and accumulates send/receive bytes per PID; the sampler reads and resets the counters each tick. Requires elevation – when not elevated the column shows "–" |
| Status (Suspended / Efficiency mode / Not responding) | Thread wait reasons from snapshot; `GetProcessInformation(ProcessPowerThrottling)`; `IsHungAppWindow` |
| App vs Background vs Windows grouping | Process owns a visible top-level window (`EnumWindows`) → App; system path + known list / session 0 → Windows process; else Background. Child processes grouped under their app via parent PID |
| Architecture, elevation, UAC virtualization | `IsWow64Process2`, token queries |

Actions: End task (`WM_CLOSE` for apps, then `Process.Kill`), End process tree (`Process.Kill(entireProcessTree: true)`), Efficiency mode (`SetProcessInformation` power throttling + `PriorityClass.Idle`), Set priority / affinity (`Process.PriorityClass`, `Process.ProcessorAffinity`), Suspend/resume (`NtSuspendProcess`), Create dump file (`MiniDumpWriteDump`), Open file location (`explorer /select,`), Search online (`Process.Start` with Bing URL), Properties (`SHObjectProperties`), Restart Windows Explorer, Run new task (dialog → `Process.Start` with `UseShellExecute`, optional "runas" / un-elevated launch).

### Performance
| Page | Source |
|---|---|
| CPU | `GetSystemTimes` / `NtQuerySystemInformation(SystemProcessorPerformanceInformation)` for total & per-core; `GetLogicalProcessorInformationEx` for sockets/cores/caches; `CallNtPowerInformation(ProcessorInformation)` for speed; base speed & name from registry `HARDWARE\DESCRIPTION\System\CentralProcessor\0`; uptime `Environment.TickCount64`; virtualization from `IsProcessorFeaturePresent` |
| Memory | `GlobalMemoryStatusEx`, `GetPerformanceInfo` (committed, cached, paged/non-paged pool); hardware reserved = installed (`GetPhysicallyInstalledSystemMemory`) − visible; speed/slots/form factor from SMBIOS via `GetSystemFirmwareTable('RSMB')` (type 17 records – avoids WMI) |
| Disk | PDH `\PhysicalDisk(*)\% Idle Time`, `Disk Read/Write Bytes/sec`, `Avg. Disk sec/Transfer`; capacity/model/SSD-vs-HDD via `DeviceIoControl(IOCTL_STORAGE_QUERY_PROPERTY)`; volumes via `DriveInfo` (BCL) |
| Ethernet / Wi‑Fi | `System.Net.NetworkInformation.NetworkInterface` (BCL) – name, type, speed, IPv4/IPv6, byte counters → send/receive rate |
| GPU | DXGI adapter enumeration (`CreateDXGIFactory1`, COM interop) for name/dedicated/shared memory; PDH `\GPU Engine(*)\Utilization Percentage` per engine type (3D, Copy, Video Encode/Decode), `\GPU Adapter Memory(*)` for usage |

UI: left list of mini-graphs (like Task Manager) + detail panel with a large graph and stat grid. CPU view can toggle "Overall utilization" vs "Logical processors" (`ItemsRepeater` + `UniformGridLayout` of small graphs).

### Startup apps
- Registry: `HKCU`/`HKLM` `Software\Microsoft\Windows\CurrentVersion\Run` (+ `WOW6432Node`), via `Microsoft.Win32.Registry` (BCL).
- Folders: `Environment.SpecialFolder.Startup` / `CommonStartup`; resolve `.lnk` targets via `IShellLinkW` COM interop.
- Enabled/disabled state: `...\Explorer\StartupApproved\Run|Run32|StartupFolder` binary values (byte 0: `02` = enabled, `03` = disabled + FILETIME). Enable/disable writes these values, exactly like Task Manager.
- Columns: Name, Publisher, Status, Location; "Startup impact" shown as "Not measured".

### Services
- `OpenSCManager` + `EnumServicesStatusEx` (name, display name, status, PID), `QueryServiceConfig2` (description), service group from registry. (`ServiceController` is a separate NuGet package in modern .NET, so plain P/Invoke is used – see §8.)
- Actions: Start, Stop, Restart, Open Services (`services.msc`), Search online, Go to details (select PID on Details page).

## 6. UI design

- **Shell**: `MainWindow` with Mica backdrop, custom title bar (`ExtendsContentIntoTitleBar`) containing app icon, title and a global **search box** (filters the current page) – matches Windows 11 Task Manager.
- **Navigation**: `NavigationView` (left, compact) → Processes, Performance, Startup apps, Details, Services; Settings in footer. Each page has a `CommandBar` with context actions (Run new task, End task, Efficiency mode, …).
- **Tables**: WinUI has no built-in DataGrid. Build one small reusable `DataTable` control: a header row (sortable, resizable `ColumnDefinition`s, right-click to pick columns) above a virtualized `ListView` whose item template uses the same column widths. Used by Processes, Details, Services and Startup.
  - Processes uses the same control over a **flattened tree** (group headers + expandable app rows with child processes) – far faster than `TreeView` for hundreds of rows.
  - Heat-map cell background (light yellow → orange) via a value converter on usage %.
- **Graphs**: `PerformanceGraph` control – a `Canvas` with grid lines and a filled `Path` built from a ring buffer of the last 60 samples. No charting library needed.
- **Context menus**: `MenuFlyout` on rows mirroring Task Manager's menus.
- **Keyboard**: Delete = End task, Ctrl+F = search, F5 = refresh now, Ctrl+1..6 = switch pages.
- Light/Dark/High-contrast via `ThemeResource` only; strings in `Resources.resw` from day one (localizable).

## 7. Milestones

Each milestone ends with a buildable, runnable app.

1. ✅ **Scaffolding** – solution, `Directory.Build.props`, `Directory.Packages.props` (central package versions for the approved packages), `.editorconfig`, WinUI app + Core lib + xUnit test project, CsWin32 wired up in Core, `app.manifest` (highestAvailable), shell with `NavigationView`, empty pages, title bar, settings persistence, MVVM base classes, `.gitignore`, README.
2. **Sampling + Details page** – `SamplingService`, `ProcessProvider` (NtQuerySystemInformation), CPU % delta logic, `DataTable` control, Details page with sorting, column picker, search filter, End task / End tree / priority / affinity / open file location.
3. **Processes page** – grouping (Apps/Background/Windows), parent/child tree, icons, heat-map, status column, efficiency mode, suspend, run new task, restart Explorer, create dump file, summary totals in column headers.
4. **Performance page** – `PerformanceGraph`, CPU (overall + logical processors), Memory (incl. composition bar), Disk, Network, GPU; per-process Disk, GPU and Network (ETW/TraceEvent) columns added to Processes/Details.
5. **Services page** – list, actions, go to details.
6. **Startup apps page** – registry + folders, enable/disable, open file location.
7. **Settings & polish** – default start page, update speed, always on top, hide when minimized, theme; keyboard shortcuts; accessibility pass (AutomationProperties, narrator); performance tuning (target < 1% CPU at Normal speed); release build & publish profile.

## 8. Decisions

### Approved packages

| Package | Project | Purpose |
|---|---|---|
| `Microsoft.WindowsAppSDK` | App | WinUI 3 |
| `Microsoft.Windows.SDK.BuildTools` | App | Windows SDK build tooling for WinUI |
| `Microsoft.Windows.CsWin32` | Core | Build-time generation of Win32 P/Invoke signatures/structs (no runtime dependency) |
| `Microsoft.Diagnostics.Tracing.TraceEvent` | Core | ETW consumer for the per-process network column |
| `xunit.v3` | Tests | Unit testing. Runs on Microsoft Testing Platform (opted in via `global.json`), so `Microsoft.NET.Test.Sdk` / `xunit.runner.visualstudio` are not needed |

Versions are pinned centrally in `Directory.Packages.props`. Any package beyond this list still needs approval.

### Deliberately not used
- `CommunityToolkit.Mvvm` – hand-written `ObservableObject`/`RelayCommand` (~60 lines) is enough. Easy to swap in later if property boilerplate grows, but needs approval first.
- `System.ServiceProcess.ServiceController`, `System.Diagnostics.PerformanceCounter`, `System.Management` – replaced by SCM/PDH P/Invoke and SMBIOS parsing.
- Charting / DataGrid libraries – replaced by the small `PerformanceGraph` and `DataTable` controls.

### Other decisions (defaults – say if you want otherwise)
- **Packaging**: unpackaged, self-contained Windows App SDK, `highestAvailable` elevation (like Task Manager).
- **Minimum OS**: Windows 11 (22000+). Lets us use efficiency mode, Mica and the title bar APIs without fallbacks.
- **Per-process network column**: included (via TraceEvent), in milestone 4.
