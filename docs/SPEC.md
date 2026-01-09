# SPEC.md

## Product
- CopyOps Suite is a WinUI 3 (.NET 8) shell for multi-destination copying, billing, and auditing operations.
- Internal modules: MultiCopy Engine for transfers, CashOps for billing/cash tracking, AuditOps for event streams and alerts.
- UI tabs: Copy, Cash, Audit, History, Settings (all within the NavigationView shell).

## Navigation
- A `NavigationView` controls the five tabs inside `MainWindow`; each view loads into the central `Frame` so only mini-transfer windows open for per-destination status.
- The Copy view shows origin/source controls, a device grid, quick actions, and a footer with live logs plus a RAM usage badge.

## Stack
- WinUI 3 (Windows App SDK) / .NET 8, CommunityToolkit.Mvvm for MVVM helpers.
- SQLite via `Microsoft.Data.Sqlite` (Core library) for persistence of settings, transfers, sales, events, etc.
- Concurrency built on Tasks, CancellationToken, and optional Channels/BlockingCollection for queueing by destination.

## Repo layout
- `src/App.WinUI`: WinUI 3 project that hosts the NavigationView shell, pages (CopyView, CashView, AuditView, HistoryView, SettingsView), MiniTransfer/JobDetail windows, controls, and viewmodels.
- `src/Core`: class library with `Models`, `Storage`, `System`, `AuditOps`, `CashOps`, and `MultiCopyEngine` folders for reusable services and domain contracts.

## Storage
- A single SQLite file lives at `%LocalAppData%/CopyOpsSuite/app.db`; `SqliteDb` bootstraps the schema on startup and seeds defaults.
- Tables: `settings`, `devices_protected`, `exclusions`, `profiles`, `transfer_jobs`, `transfer_targets`, `transfer_items`, `validations`, `errors`, `ram_stats`, `sales`, `cash_register`, `events`, `sessions`, `pricing_tiers` (all columns reflect the Core models).
- Seeded defaults include the exclusions `thumbs.db`, `desktop.ini`, `*.tmp`, plus a `PROFILE_DEFAULT` entry and mini-window settings (`mini_auto_open`, `mini_auto_close_no_errors`, `mini_auto_clean_no_errors`, `mini_dock_side`, `mini_top_margin`, `mini_opacity_percent`, `mini_simple_mode_default`, `max_parallel_targets`, `max_files_parallel_per_target`).
- `Repositories` exposes async helpers (settings, profiles, jobs, targets, logs, sales, events, sessions, tiers, etc.) for higher layers.

## Next steps
- Wire each viewmodel to live services in Core (navigation, logging, metrics, persistence).
- Implement MultiCopy workflow (file enumeration, exclusions, target queues, mini windows, logging) plus billing and audit pipelines.
- Finish SQLite schema migration, CSV exports, and MSIX packaging for deployment.

## Stage 3 UI Notes
- Copy view keeps a fixed `DeviceRows` collection backed by `DeviceRowViewModel`, displays health/state chips, throttled progress updates, tooltips, and quick-checkout accounting data without recreating rows on every engine tick. Mini-transfer windows now respect the docking, opacity, and topmost settings, arrange themselves along the working-area edges, and auto-open/close based on job outcomes.
- Precheck handling reports PASS/WARN/FAIL for space, FAT32, USB2, and source validation; the dialog still only allows forcing a job when an admin unlocks the interface.
- Job detail windows expose a pivot of list items, validations, errors, RAM stats, and timeline events pulled directly from the repositories and the aggregated audit stream.
- Settings exposes an Access Mode section with operator/admin toggles, PIN-backed lockouts, buffering controls, diagnostics logging, and admin-only badges when advanced sections are restricted; diagnostics refresh devices, emit `DIAG_*` audit events, and can exercise a 64 MB buffer test that feeds Cash and Audit reports.
- Cash view groups today's data by currency, surfaces the latest diagnostics report, keeps the sales grid with rounding transparency, and offers quick checkout insights plus context actions that open jobs or jump to Audit filters.
- Audit view now supports full event filtering, alert cards with resolved persistence, simulation tooling that honors configurable USB baselines, and context menus that open related jobs/sales or copy event details.
- History view lists completed jobs with target and byte summaries, highlights expected/paid gaps, and exposes actions to open job details, focus the related sale, or filter Audit events.

## Pricing tiers (CUP)
- Pricing uses volume tiers with a hard CAP tier.
- billableGB = ceil(bytes / 1024^3). Any bytes > 0 bill at least 1 GB.
- The first matching tier is applied by MinGB/MaxGB. MaxGB = 0 means infinity.
- The CAP tier is the one with MinGB >= 1024 and MaxGB = 0. Any billableGB >= 1024 uses this same price.
- Example: 1 TB (1024 GB) and 60 TB both use the CAP tier price.
