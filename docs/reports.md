# Reports and auditing

Reports are written to `C:\ProgramData\OemCleanup\Reports` unless `reportDirectory`
specifies another location. They are produced on **every** run, including audit-only
runs where nothing is removed.

## Artifacts

| File | Purpose |
| --- | --- |
| `*.json` | Machine-readable schema 2.2 for RMM/MSP ingestion |
| `*.html` | Human-readable device, inventory, decision, and execution report |
| `*.jsonl` | Hash-chained audit events |
| `*-execution.jsonl` | Commands, results, errors, exit codes, reboot flags, post-execution inventory |

## What a report contains

- **Device context** — hostname, user context, local-admin status, Windows version,
  manufacturer, model, BIOS version, serial number, reboot-pending state
- **Security context** — Security Center antivirus inventory
- **Inventory** — full installed-application inventory
- **Decisions** — catalog matches, match confidence, evidence, risk classification, and
  the resulting policy decision for each item
- **Execution** — before/after counts, exit codes, timeout and failure results, captured
  uninstaller output, reboot indicators, and item-specific rollback guidance

Failed uninstallers preserve bounded diagnostic output in the result, so a report shows
the vendor or Windows deployment error rather than only an exit code.

## Tamper evidence

Each JSONL event embeds the hash of the previous event, forming a chain. Whole-log
SHA-256 checksums are recorded in the report. Removing or editing an event breaks the
chain and is detectable.

This makes a run defensible after the fact: you can show what was on the device, what
the tool decided, why it decided it, and what actually happened.

## Exit codes

Standard Windows Installer semantics are preserved and classified distinctly:

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1603` | Fatal error during installation (vendor failure) |
| `3010` | Success, reboot required |
| `1641` | Success, reboot initiated |
| timeout | Reported as a separate outcome, not as an installer failure |

## RMM ingestion

The JSON report is the intended integration surface. Schema version is recorded in the
document so ingestion can branch on it.

Because reports contain serial numbers and full software inventory, restrict report
directory ACLs and RMM ingestion access according to organizational policy.

## Rollback

Vendor uninstallers are not transactional and the utility does not create restore points
or attempt file/registry reconstruction. For successfully removed software, reinstall
from the OEM support portal, the Microsoft Store, or your approved package source. The
report records item-specific reinstall guidance.
