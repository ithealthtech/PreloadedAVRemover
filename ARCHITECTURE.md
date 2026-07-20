# Architecture

## Original application

Version 1.2 was a single WinForms source file. The form directly enumerated uninstall registry keys, matched antivirus names, parsed registry commands, started uninstallers, and rendered status. It had no durable domain model, policy engine, hardware inventory, structured catalog, report schema, tamper-evident log, or automated tests.

The direct UI-to-process path was the highest-risk boundary: command handling, product classification, user selection, and execution were coupled and could not be tested independently.

## Version 2 integration

The WinForms layout remains the presentation layer. All detection, policy, audit, validation, reporting, and execution behavior now lives behind testable core services:

```text
WinForms UI
  -> WindowsInventoryProvider
  -> RemovalCatalog + PolicyEvaluator
  -> CleanupEngine
       -> CommandValidator
       -> IProcessRunner
       -> HashChainAuditLogger
  -> ReportWriter (JSON + HTML)
```

### Components

- `Core/Models.cs`: stable inventory, catalog, policy, plan, result, device, and report contracts.
- `Core/WindowsInventoryProvider.cs`: registry, WMI, Security Center, AppX, service, scheduled-task, manufacturer, BIOS, serial, admin, and reboot inventory.
- `Core/CatalogPolicy.cs`: embedded catalog loading, brand/product matching, policy profiles, allow/block lists, and protected-software guards.
- `Core/SecurityExecution.cs`: fail-closed command validation and direct process execution abstraction.
- `Core/AuditReporting.cs`: hash-chained JSONL logging and JSON/HTML report generation.
- `Core/CleanupEngine.cs`: audit, dry-run/removal orchestration, result capture, and post-execution inventory.
- `Core/Configuration.cs`: organization policy loading with conservative dry-run fallback.
- `Catalog/oem-removal-catalog.json`: structured, reviewable removal metadata.
- `tests/PreloadedAVRemover.Tests`: unit, mock, negative, regression, and read-only integration tests.

## Trust boundaries

Installed-app metadata and registry uninstall strings are untrusted input. Catalog matching alone never executes a command. Policy must authorize removal, the selected backend must support safe automation, elevation must be present, and command validation must succeed before `IProcessRunner` receives a process specification.

Protected software and security products are evaluated before organization blocklists. This prevents a broad wildcard from removing RMM, VPN, backup, encryption, drivers, firmware, or active endpoint protection.

