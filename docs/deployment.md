# Deployment

## Choosing a mode

| Mode | Use when |
| --- | --- |
| Per-machine MSI | Standard fleet deployment through RMM or Group Policy |
| Per-user MSI | The technician cannot install per-machine software |
| Portable ZIP | One-off remediation on a device you will not enroll |

## Unattended MSI

Standard Windows Installer properties apply.

All users (elevated):

```powershell
msiexec.exe /i .\OEM-Endpoint-Cleanup-2.2.1-win-x64.msi /qn /norestart ALLUSERS=1 MSIINSTALLPERUSER=""
```

Current user:

```powershell
msiexec.exe /i .\OEM-Endpoint-Cleanup-2.2.1-win-x64.msi /qn /norestart ALLUSERS=2 MSIINSTALLPERUSER=1
```

## Verify the download

Every release publishes `SHA256SUMS.txt`. Verify before deploying:

```powershell
Get-FileHash .\OEM-Endpoint-Cleanup-2.2.1-win-x64.msi -Algorithm SHA256
```

Compare the result against the published manifest.

## Deploy policy before the tool

Push [`policy.json`](configuration.md) to `C:\ProgramData\OemCleanup\` as part of the
same deployment. This ensures every technician runs the same profile, allowlist, and
report directory, rather than relying on per-run UI choices.

Treat `policy.json` as a privileged file. Anyone who can write it can set `force: true`
and suppress the confirmation prompt for already-authorized actions. Restrict its ACL to
administrators.

## Suggested rollout

1. Deploy to a small hardware ring covering each OEM model you support.
2. Run in dry-run and collect the JSON reports centrally.
3. Review catalog matches and decisions per model before enabling uninstall mode.
4. Widen the ring only after a model's results are stable.

OEM product names, installer technology, and supported silent flags vary by model and
release, so ring testing matters more here than for typical software.

## SmartScreen

The published executable is not code-signed and may trigger SmartScreen. For production
distribution, re-sign the artifacts with an organization-controlled certificate.

## Collecting results

Point `reportDirectory` at a path your RMM collects, and ingest the `*.json` artifact.
See [Reports and auditing](reports.md) for the schema and the sensitivity note.
