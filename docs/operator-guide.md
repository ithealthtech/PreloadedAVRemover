# Operator guide

This guide assumes you are a technician running the utility against a device you are
authorized to administer.

## Before you start

- The device must be Windows x64 and you must be able to elevate.
- A first run changes nothing. You can safely run it on a production machine to produce
  an inventory before deciding anything.
- If you manage the fleet centrally, deploy a [policy file](configuration.md) first so
  every technician gets the same profile and allowlist.

## A safe run, step by step

1. Start `PreloadedAVRemover.exe` and approve UAC.
2. Review the automatic audit. JSON and HTML reports are written immediately.
3. Select a policy profile and one or more rows whose decision is **Remove**.
4. Leave **Uninstall mode** unchecked and choose **Preview selected** to validate the
   plan without changing the device.
5. To make changes, enable **Uninstall mode**, choose **Uninstall selected**, and approve
   the confirmation dialog.
6. Review the post-execution inventory, results, exit codes, and reboot indicators in the
   exported report.

Steps 1-4 are non-destructive. The device is only modified at step 5.

## Reading the grid

The main grid shows friendly product and status labels plus a color-coded category for
quick triage:

| Category | What it means |
| --- | --- |
| Antivirus / Security | Endpoint protection - audit-only unless the security gate is enabled |
| OEM Control Panel | Vendor settings/hardware-control surface - usually preserved |
| Hardware / Recovery | Firmware, driver, or recovery dependency - preserved by policy |
| Trialware | Time-limited bundled software |
| Consumer App | Games, media, and consumer promotions |
| Bloatware | Promotional, registration, and welcome utilities |
| OEM Support / Updates | Vendor support assistants and update managers |
| Background Component | Telemetry and customer-experience agents |
| OEM Utility | Other vendor-supplied tooling |

Hover a category for its meaning, or a product name for its exact technical name and
identifier. Exported reports always retain the original untruncated values.

## The two authorization gates

Some software is deliberately harder to remove than a checkbox.

**Endpoint protection** stays audit-only unless **Include security apps** is explicitly
enabled in uninstall mode. Enabling it does not override allowlists, protected-software
safeguards, manual-review catalog entries, or command validation.

**Remote-management tools** appear in a dedicated investigation panel on the right, not
in the main list. Removing one requires all of: uninstall mode, the separate **Include
remote tools** authorization, an eligible policy decision, row selection, and final
confirmation. Under `Conservative` policy an organization blocklist match is
additionally required.

ConnectWise and ScreenConnect are classified as approved management software. They are
preserved even with remote-tool removal and an aggressive organization blocklist both
enabled - there is no control that promotes them to removal.

## When an item will not remove

If a row is `manual-review` or skipped, the report records the reason. The common ones:

- **Low or tied match confidence** - the tool cannot prove which product it is.
- **Protected software** - it matched an RMM, VPN, backup, driver, or firmware guard.
- **No supported backend** - the item is a service, scheduled task, or registry artifact
  without a dedicated tested handler.
- **Gate not enabled** - it is endpoint protection or a remote tool and the matching
  authorization was off.
- **Command validation failed** - the vendor uninstall string was not safely executable
  (see [Security model](security-model.md)).

These are fail-closed outcomes by design, not bugs. If you believe an item should be
cataloged, see [Catalog](catalog.md).

## After the run

Reports are your evidence. See [Reports and auditing](reports.md) for the artifacts, the
hash-chained audit log, and exit-code meanings.

Removal is not transactional and there is no automatic rollback. Reinstall removed
software from the OEM support portal, the Microsoft Store, or your approved package
source; the report records item-specific guidance.
