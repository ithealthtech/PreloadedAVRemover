## What does this change?

<!-- Explain why, not just what. The diff already says what. -->

## Type of change

- [ ] Catalog entry (addition, reclassification, or protection)
- [ ] Bug fix
- [ ] New capability
- [ ] Documentation
- [ ] Build, packaging, or CI

## Safety review

- [ ] This change does **not** widen what the tool is willing to remove.
- [ ] If it does widen eligibility, I have explained why in the description and added
      tests covering the new path.

Which design rules from [CONTRIBUTING.md](../CONTRIBUTING.md) does this touch?

<!-- e.g. "Rule 4 - protected software ordering" or "none" -->

## Verification

```powershell
dotnet format --verify-no-changes
dotnet build .\PreloadedAVRemover.csproj -c Release
dotnet test .\tests\PreloadedAVRemover.Tests\PreloadedAVRemover.Tests.csproj -c Release
```

- [ ] Format check passes
- [ ] Release build is warning-free
- [ ] Full test suite passes
- [ ] Tests added for the changed behavior
- [ ] Removal paths use the mocked `IProcessRunner` (no test uninstalls real software)

## Documentation

- [ ] `docs/` updated where behavior changed
- [ ] `pages/` updated if the change is user-facing
- [ ] `CHANGELOG.md` updated under `[Unreleased]`

## Hardware testing

<!-- Catalog changes should be ring-tested. Which OEM brands and models did you verify against? -->
