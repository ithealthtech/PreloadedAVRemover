# Test report — OEM Endpoint Cleanup 2.2.1-rc.2

Date: 2026-07-22
Target: Windows x64, .NET 8
Command: `dotnet test .\tests\PreloadedAVRemover.Tests\PreloadedAVRemover.Tests.csproj -c Release --no-restore`

## Result

- Passed: 65
- Failed: 0
- Skipped: 0
- Build warnings: 0 after analyzer cleanup

## Tested areas

- Embedded catalog loads and covers every required OEM brand.
- Catalog regression coverage includes HP QuickDrop, ASUS GlideX, Lenovo Smart Appearance, Samsung Galaxy Book Experience, and Razer Axon.
- Catalog includes MSI, EXE, AppX/MSIX, winget, service, scheduled-task, and registry-entry package types.
- Brand and product matching uses manufacturer and publisher evidence.
- Match confidence and rationale are emitted; tied candidates fail closed as manual review.
- Duplicate catalog IDs and malformed catalog regular expressions are rejected.
- MSI/EXE registry classification fallback works without duplicate exact matches.
- Conservative, balanced, and aggressive risk rules.
- Allowlist precedence and blocklist force-eligibility behavior.
- Protected RMM, remote-access, VPN, backup, BitLocker/firmware/driver-style software remains skipped.
- Endpoint protection is audit-only by default and becomes eligible only after explicit authorization.
- Active Security Center protection is guarded independently from catalog security metadata.
- Existing McAfee AV matching/removal policy regression.
- MSI commands are rebuilt from validated product GUIDs.
- Existing direct EXE commands, including unquoted paths containing spaces, are parsed without a shell.
- Malformed, relative, missing, control-character, shell-host, and script-host uninstall commands fail closed.
- Environment-expanded registry paths are rejected and command timeouts are clamped to 30–3600 seconds.
- Valid and malicious AppX package identifiers.
- AppX package arguments are passed separately through a fixed script-block parameter, and registered users are removed explicitly.
- Unavailable winget behavior.
- Service, scheduled-task, and registry backends fail closed without dedicated handlers.
- Mock inventory matching for every package type.
- Dry-run never invokes the process runner.
- Non-admin removal fails without invoking the process runner.
- Success, failure (`1603`), reboot-required (`3010`), and initiated-reboot (`1641`) exit paths.
- Process timeouts are captured as a distinct auditable outcome.
- Failed uninstallers preserve bounded diagnostic output in the execution result and report.
- Empty inventory/missing-source behavior.
- Hash-chain linkage, hostname context, and whole-file SHA-256 generation.
- JSON report schema 2.2, HTML escaping, match evidence, and complete device/security context.
- Policy loading, missing policy defaults, and malformed-policy fail-closed behavior.
- Read-only integration inventory against the local Windows registry, WMI, AppX metadata, services, and scheduled tasks.
- Installer-mode argument generation for per-machine and per-user MSI installs.
- Portable package checksum validation, normal extraction, traversal rejection, and symbolic-link rejection.
- Branded setup launcher layout self-test and adjacent MSI/ZIP payload verification.
- WiX MSI build validation with zero warnings and zero errors.

## Remaining risks

- OEM product names, installer technology, and supported silent flags vary by model and release. Catalog updates should be hardware-ring tested before broad aggressive deployment.
- Vendor uninstallers can be password-protected, self-protected, interactive, or return nonstandard exit codes.
- No automated destructive test was run against real OEM applications; removal tests use mocked process execution by design.
- Services, scheduled tasks, and registry artifacts are audit/manual-review only until an exact vendor-specific handler is cataloged and tested.
- AppX removal behavior varies across Windows editions and provisioned-versus-installed package state.
- Vendor uninstallation is not transactional; automatic rollback is not generally available.
- The self-contained executable is not code-signed and may trigger SmartScreen. Production distribution should use an organization-controlled signing certificate and release pipeline.
- Serial numbers are operationally sensitive. Restrict report ACLs and RMM ingestion access according to organizational policy.
