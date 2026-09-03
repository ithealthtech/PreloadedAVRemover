# Security policy

## Supported versions

| Version | Supported |
| --- | --- |
| 2.2.x | Yes |
| 2.1.x and earlier | No |
| 1.x | No |

Version 1.x had no policy engine, command validation, or audit log. Upgrade rather than
report issues against it.

## Reporting a vulnerability

Report privately. Do **not** open a public issue for a security defect.

Use [GitHub private vulnerability reporting](https://github.com/ithealthtech/PreloadedAVRemover/security/advisories/new).
This is the preferred channel and it notifies the maintainers directly.

Please include:

- The version and how it was installed (per-machine MSI, per-user MSI, or portable).
- The policy file in effect, with any sensitive paths redacted.
- What you expected the safety model to prevent, and what happened instead.
- A reproduction, including the relevant registry uninstall entry if the issue involves
  matching or command validation.
- The relevant report and audit-log excerpts if you have them.

We will acknowledge within 5 business days and aim to give you a remediation timeline
within 15 business days.

## What we consider a vulnerability

This project's security value is that it refuses to act when it cannot prove it is safe
to. Anything that defeats that is in scope:

- Removing or bypassing a protected-software guard (RMM, remote access, VPN, backup,
  encryption, drivers, firmware, active endpoint protection).
- Causing a removal without the required authorization gate, policy eligibility,
  elevation, or confirmation.
- Escaping command validation — reaching a shell, a script host, a relative or
  network-hosted path, or an environment-expanded path.
- Causing execution from a low-confidence, ambiguous, or unmatched item.
- Promoting ConnectWise or ScreenConnect to a removal decision.
- Forging, breaking, or silently truncating the hash-chained audit log.
- Privilege escalation via `policy.json`, the report directory, or the installer.
- Path traversal or symlink escape in portable extraction.

## Known limitations, not vulnerabilities

These are documented design boundaries. Please do not report them as vulnerabilities.

- **The published executable is not code-signed** and may trigger SmartScreen.
  Production distribution should use an organization-controlled signing certificate.
- **`force: true` suppresses the final confirmation.** This is intended for centrally
  managed deployment. Anyone who can write `policy.json` can set it, which is why that
  file must be ACL-restricted to administrators.
- **Reports contain sensitive data** — hostname, serial number, BIOS version, and full
  software inventory. Protecting the report directory is the operator's responsibility.
- **Vendor uninstallers are not transactional** and there is no automatic rollback.
- **Vendor uninstallers may misbehave** — being password-protected, self-protected,
  interactive, or returning nonstandard exit codes is a vendor property, not a defect
  here.
- **Services, scheduled tasks, and registry artifacts are audit-only.** They fail closed
  by design until a dedicated tested handler exists.

## Disclosure

We prefer coordinated disclosure. Once a fix ships we will credit you in the release
notes unless you ask us not to.
