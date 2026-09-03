# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Restructured project documentation: the README is now an entry point, operator and
  developer documentation moved into `docs/`, and an end-user documentation site is
  published to GitHub Pages from `pages/`.
- `ARCHITECTURE.md` moved to `docs/architecture.md`; `TEST_REPORT.md` moved to
  `docs/test-report.md`.

### Added

- `SECURITY.md`, `CONTRIBUTING.md`, and this changelog.
- Issue and pull request templates, including a dedicated catalog request form.

## [2.2.1] - 2026-07-22

Stable production release.

### Added

- Friendly application names replace package and service identifiers in the main grid,
  with exact technical names retained in tooltips and audit reports.
- Color-coded categories for antivirus/security, OEM control panels, hardware/recovery
  tools, trialware, consumer apps, promotional bloatware, OEM support/update tools, and
  background components.
- Setup launcher offering **Everyone on this computer**, **Just me**, and **Portable**
  installation modes.
- IT Health Tech branding across the application, taskbar, setup launcher, MSI, and
  copyright footer.

### Changed

- Responsive layout reduces crowding in the main window.

### Security

- Dry-run remains the default; endpoint protection still requires explicit authorization
  and confirmation.
- Drivers, firmware dependencies, hotkeys, RMM, VPN, backup, and BitLocker tooling remain
  safeguarded independently of organization policy.

### Verified

79/79 automated tests passed. Release build and full MSI/setup/portable packaging
completed with zero warnings and zero errors. Application and setup UI self-tests passed.
SHA-256 manifest independently revalidated. Microsoft Defender found no threats in the
published artifacts.

## [2.2.0] - 2026-07-20

Reworked the utility from a single-purpose antivirus remover into an audit-first OEM
cleanup tool for MSP onboarding and controlled endpoint deployment.

### Added

- Catalog-driven inventory across 63 entries covering major PC manufacturers, bundled
  security trials, support utilities, telemetry, promotional apps, AppX packages,
  services, scheduled tasks, and registry artifacts.
- Conservative, Balanced, and Aggressive policy profiles, with dry-run enabled by
  default.
- Confidence scoring and evidence for catalog matches; ambiguous or low-confidence
  matches fail closed to manual review.
- Process timeouts, exit-code capture, reboot reporting, before/after inventory, and
  hash-chained JSONL audit logs.
- MSP-ready JSON and HTML reports.
- Organization policy file support (`policy.json`) with allow/block lists.

### Security

- Independent safeguards for Microsoft Defender, active endpoint protection, RMM agents,
  VPNs, backup tools, firmware, drivers, hotkeys, recovery, and warranty-critical
  software.
- Strict MSI, EXE, AppX/MSIX, and winget command validation. Registry uninstall strings
  are never executed through a command shell.

### Known issues

- The executable is not code-signed and may trigger Microsoft SmartScreen.
- Vendor uninstallers vary and may require passwords, vendor cleanup tools, or manual
  intervention.
- Uninstallation is not transactional; review the generated rollback guidance and test
  against representative hardware before broad deployment.

### Verified

57 tests passed with 0 failures. Release build and WinForms layout self-test passed.
Direct and transitive NuGet vulnerability audits reported no known vulnerable packages.
Windows x64 self-contained single-file publish passed.

## [1.2.0] - 2026-07-19

Initial public release, as Preloaded AV Remover.

### Added

- Detection of common OEM-bundled antivirus products.
- Registered silent removal where available, with fallback to vendor uninstall prompts.
- Post-removal verification of what remains.

### Security

- Microsoft Defender is never targeted.

This version had no policy engine, command validation, catalog, or audit log. It is no
longer supported; see [SECURITY.md](SECURITY.md).

[Unreleased]: https://github.com/ithealthtech/PreloadedAVRemover/compare/v2.2.1...HEAD
[2.2.1]: https://github.com/ithealthtech/PreloadedAVRemover/compare/v2.2.0...v2.2.1
[2.2.0]: https://github.com/ithealthtech/PreloadedAVRemover/compare/v1.2.0...v2.2.0
[1.2.0]: https://github.com/ithealthtech/PreloadedAVRemover/releases/tag/v1.2.0
