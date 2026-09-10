# Security model

The utility runs elevated, reads attacker-influenced data, and can start vendor
executables. The design assumes that any of those inputs may be hostile and that a
wrong removal is more costly than a missed one.

## Trust boundaries

Installed-application metadata and registry uninstall strings are **untrusted input**.
A catalog match alone never causes execution. Every one of the following must succeed,
in order, before `IProcessRunner` receives a process specification:

1. The item matches a catalog entry with sufficient confidence and no ambiguity.
2. The active policy profile authorizes removal for that entry's risk classification.
3. Any applicable authorization gate is explicitly enabled (see below).
4. The removal backend has a dedicated, tested handler.
5. The process is elevated.
6. Command validation succeeds.
7. The operator selected the row and confirmed, unless `force` is set by managed policy.

Failure at any step yields a skipped or `manual-review` outcome, and the reason is
recorded in the report.

## Protected software is evaluated before organization policy

Protected-software guards run **before** the organization blocklist. A broad wildcard
such as `*` in a blocklist cannot reach RMM agents, remote-access tools, VPN clients,
backup agents, encryption and BitLocker tooling, drivers, firmware, or active endpoint
protection. This ordering is deliberate: a blocklist is an operator convenience, not a
privilege escalation.

ConnectWise and ScreenConnect are built-in approved-management classifications. They
never become removal decisions, even with remote-tool authorization enabled and an
aggressive wildcard blocklist.

## The two authorization gates

Two categories are audit-only by default and require a dedicated opt-in that is separate
from uninstall mode:

| Gate | Governs | Additional constraint |
| --- | --- | --- |
| **Include security apps** | Endpoint protection products | Does not override allowlists, protected-software guards, manual-review entries, or command validation |
| **Include remote tools** | Cataloged remote-management tools | Conservative policy additionally requires an organization blocklist match; ConnectWise/ScreenConnect remain allowlisted |

Active Security Center products are guarded independently of catalog metadata, so an
incomplete catalog entry cannot bypass the security gate.

## Command validation

Uninstall commands are never handed to a shell.

- Registry uninstall strings are parsed with `CommandLineToArgvW` and executed as a
  direct executable plus an argument vector — never through `cmd.exe`.
- Executable paths must be absolute, end in `.exe`, and exist on disk.
- Environment-expanded and network-hosted registry executable paths are rejected.
- Shells and script hosts sourced from registry commands are rejected.
- MSI removal is rebuilt as `msiexec /x {ProductCode} /qn /norestart` from a validated
  GUID rather than reusing the vendor string.
- AppX and winget use fixed handlers with validated package identifiers. AppX removal
  passes the package name as a separate script-block argument and uses the Windows
  all-users package-removal API.
- Services, scheduled tasks, and registry artifacts are detected and audited but fail
  closed unless a dedicated catalog handler exists.
- No arbitrary file or registry deletion is ever performed.
- Non-admin removal attempts fail before a process is started.

## Matching safety

Matches carry a 0-100 confidence score plus human- and machine-readable rationale.
Low-confidence matches and tied highest-confidence candidates become `manual-review`
decisions and never reach command validation. Catalog duplicates and malformed regular
expressions are rejected at load time.

## Bounded execution

Each validated uninstaller is bounded by `processTimeoutSeconds`, clamped to 30-3600
seconds with a default of 900. A timeout terminates the process tree where Windows
permits it and is reported as a distinct outcome, separate from an installer failure.

## Sensitive output

Reports contain hostname, user context, serial number, BIOS version, and a full
installed-software inventory. Treat report directories as sensitive: restrict ACLs on
`C:\ProgramData\OemCleanup\Reports` and control RMM ingestion access according to
organizational policy.

## Known limitations

- The published executable is not code-signed and may trigger SmartScreen. Production
  distribution should use an organization-controlled signing certificate.
- Vendor uninstallation is not transactional; automatic rollback is not available.
- Vendor uninstallers may be password-protected, self-protected, interactive, or return
  nonstandard exit codes.

## Reporting a vulnerability

See [SECURITY.md](../SECURITY.md).
