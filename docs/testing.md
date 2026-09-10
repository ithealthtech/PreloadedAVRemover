# Testing

## Running the suite

```powershell
dotnet test .\tests\PreloadedAVRemover.Tests\PreloadedAVRemover.Tests.csproj -c Release
```

The suite is safe to run on a normal workstation. Removal paths use a mocked
`IProcessRunner`; no test uninstalls real software. A small number of read-only
integration tests query the local registry, WMI, AppX metadata, services, and scheduled
tasks.

## What is covered

Coverage is organized around the fail-closed guarantees the design depends on:

- **Catalog integrity** - every required OEM brand is present; duplicate IDs and
  malformed regular expressions are rejected at load.
- **Matching** - manufacturer and publisher evidence, confidence scoring and rationale,
  tied candidates failing closed to manual review.
- **Policy** - conservative/balanced/aggressive rules, allowlist precedence, blocklist
  force-eligibility, and the protected-software guards holding against wildcards.
- **Authorization gates** - endpoint protection audit-only by default; Security Center
  products guarded independently of catalog metadata; remote tools requiring the separate
  gate; ConnectWise/ScreenConnect remaining approved under an aggressive wildcard
  blocklist.
- **Command validation** - MSI rebuild from validated GUIDs; unquoted paths with spaces;
  rejection of malformed, relative, missing, control-character, shell-host, script-host,
  and environment-expanded commands; timeout clamping.
- **Backends** - AppX identifier validation and argument passing, winget unavailability,
  and service/scheduled-task/registry backends failing closed.
- **Execution outcomes** - dry-run never invoking the runner, non-admin failing before
  process start, and the `0` / `1603` / `3010` / `1641` / timeout paths.
- **Reporting** - hash-chain linkage, whole-file SHA-256, JSON schema 2.2, HTML escaping,
  and friendly-label/category classification.
- **Packaging** - installer argument generation, portable checksum validation, traversal
  and symlink rejection, launcher layout self-test, and WiX artwork constraints.

## Recorded results

See [test-report.md](test-report.md) for the recorded run against 2.2.1, including the
pass count and the enumerated remaining risks.

## Adding tests

New catalog entries should come with a regression test that asserts the expected match
and policy decision. New removal backends must include negative tests proving the backend
fails closed before the handler exists. See [Contributing](../CONTRIBUTING.md).
