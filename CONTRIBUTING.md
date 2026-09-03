# Contributing

Thanks for helping improve OEM Endpoint Cleanup.

This tool runs elevated on other people's production machines and decides whether to
uninstall software. The bar for changes is correspondingly high: **a change that makes
the tool more willing to act needs more evidence than a change that makes it more
cautious.**

## Before you start

- Security defects go through [SECURITY.md](SECURITY.md), not a public issue or PR.
- For a new catalog entry, read [docs/catalog.md](docs/catalog.md) first — most catalog
  requests can be filed as an issue without writing code.
- For anything larger than a catalog entry or a doc fix, open an issue first so we can
  agree on the approach.

## Development setup

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows
x64. There is no solution file; build each sub-project individually.

```powershell
dotnet build .\PreloadedAVRemover.csproj
dotnet test .\tests\PreloadedAVRemover.Tests\PreloadedAVRemover.Tests.csproj -c Release
```

See [docs/building.md](docs/building.md) for publish and installer packaging.

## Before you open a pull request

```powershell
dotnet format --verify-no-changes
dotnet build .\PreloadedAVRemover.csproj -c Release
dotnet test .\tests\PreloadedAVRemover.Tests\PreloadedAVRemover.Tests.csproj -c Release
```

CI runs the same checks plus the UI layout self-test, a single-file publish
verification, and a full packaging build. Keep the build warning-free — the recorded
baseline is zero warnings.

## Design rules that are not negotiable

These encode the tool's safety guarantees. A PR that weakens one will be declined unless
it is accompanied by a very good argument and tests.

1. **Fail closed.** An unresolved condition produces `manual-review` or a skip. It never
   produces an attempted removal.
2. **A catalog match is not permission.** Policy, the authorization gates, backend
   support, elevation, and command validation are all evaluated independently.
3. **Never route a command through a shell.** No `cmd.exe`, no script hosts, no
   environment-expanded or network-hosted paths.
4. **Protected software is evaluated before organization policy.** A blocklist must
   never be able to reach RMM, VPN, backup, encryption, driver, firmware, or active
   endpoint protection software.
5. **Presentation stays in the UI layer.** Detection, policy, validation, execution,
   audit, and reporting live behind testable services in `Core/`. WinForms code should
   not grow logic.
6. **Every removal decision is explainable.** If the tool acts, the report must record
   the match, the confidence, the evidence, and the policy reason.

See [docs/architecture.md](docs/architecture.md) and
[docs/security-model.md](docs/security-model.md) for the full picture.

## Tests

- New catalog entries need a regression test asserting the expected match and policy
  decision.
- New removal backends need negative tests proving the backend fails closed before a
  handler exists.
- Removal paths must use the mocked `IProcessRunner`. Do not add a test that uninstalls
  real software.

## Commit and PR style

- Keep the subject line imperative and under ~72 characters.
- Explain *why* in the body, not just what — the diff already says what.
- One logical change per PR. A catalog addition and a policy-engine change belong in
  separate PRs.
- Note in the PR description which of the six design rules above your change touches, if
  any.

## Documentation

Docs live in [`docs/`](docs/) for developers and operators, and in [`pages/`](pages/) for
the published end-user site at <https://ithealthtech.github.io/PreloadedAVRemover/>.
Behavior changes should update both where relevant. The README is an entry point, not a
manual — deep detail belongs in `docs/`.

## License

By contributing you agree that your contributions are licensed under
[GPL-3.0-only](LICENSE).
