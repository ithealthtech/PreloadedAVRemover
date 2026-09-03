# OEM Endpoint Cleanup documentation

Developer and operator documentation for the repository. End-user documentation is
published at <https://ithealthtech.github.io/PreloadedAVRemover/>.

## For operators

| Document | What it covers |
| --- | --- |
| [Operator guide](operator-guide.md) | Safe usage, the two authorization gates, reading a run |
| [Configuration](configuration.md) | `policy.json` schema, profiles, allow/block lists |
| [Reports and auditing](reports.md) | Output artifacts, JSON schema 2.2, hash-chained audit log |
| [Deployment](deployment.md) | Installer modes, unattended MSI, RMM rollout |

## For developers

| Document | What it covers |
| --- | --- |
| [Architecture](architecture.md) | Component layout and the version 2 integration |
| [Security model](security-model.md) | Trust boundaries, command validation, fail-closed rules |
| [Catalog](catalog.md) | Catalog format and how to propose an entry |
| [Building](building.md) | Toolchain, publish, installer packaging |
| [Testing](testing.md) | Running the suite and what is covered |
| [Test report](test-report.md) | Recorded results and remaining risks for 2.2.1 |

## Conventions used in these documents

- **Fail closed** means an unresolved condition produces `manual-review` or a skipped
  action, never an attempted removal.
- **Authorization gate** means an explicit, separate operator opt-in that policy alone
  cannot satisfy.
- Paths use the per-machine defaults; a policy file may relocate report output.
