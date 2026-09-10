# Configuration

Organization policy is optional. Without a policy file the utility runs in its safest
configuration: the `Conservative` profile with dry-run enabled.

## Policy file location

Copy [`policy.example.json`](../policy.example.json) to:

```text
C:\ProgramData\OemCleanup\policy.json
```

A malformed policy file **fails closed** to conservative dry-run behavior rather than
being partially applied.

## Example

```json
{
  "profile": "Conservative",
  "dryRun": true,
  "force": false,
  "allowSecurityProductRemoval": false,
  "allowRemoteManagementRemoval": false,
  "processTimeoutSeconds": 900,
  "allowList": ["ConnectWise*", "*ScreenConnect*", "Lenovo Vantage", "*SupportAssist*"],
  "blockList": ["WildTangent Games"],
  "reportDirectory": "C:\ProgramData\OemCleanup\Reports"
}
```

## Field reference

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `profile` | string | `Conservative` | `Conservative`, `Balanced`, or `Aggressive` |
| `dryRun` | bool | `true` | When true, no process is ever started |
| `force` | bool | `false` | Suppresses the final UI confirmation for an already-authorized action |
| `allowSecurityProductRemoval` | bool | `false` | Enables the endpoint-protection authorization gate |
| `allowRemoteManagementRemoval` | bool | `false` | Enables the remote-tool authorization gate |
| `processTimeoutSeconds` | int | `900` | Per-uninstaller bound, clamped to 30-3600 |
| `allowList` | string[] | empty | Wildcard patterns that are never removed |
| `blockList` | string[] | empty | Wildcard patterns promoted toward removal eligibility |
| `reportDirectory` | string | `C:\ProgramData\OemCleanup\Reports` | Output location |

## Policy profiles

| Profile | Automatic eligibility |
| --- | --- |
| `Conservative` | Catalog entries marked `safe` only |
| `Balanced` | `safe` plus cataloged `caution` entries, with confirmation |
| `Aggressive` | `safe` and `caution`; `manual-review` remains protected |

No profile makes `manual-review` entries eligible. `Aggressive` widens which cataloged
entries are considered, not which safety checks apply.

## Allow and block lists

Wildcards `*` and `?` are supported in both lists.

**The allowlist always wins over the blocklist.** A blocklist entry can promote a
cataloged `caution` item to removal eligibility, but it cannot bypass:

- protected-software patterns (RMM, VPN, backup, encryption, drivers, firmware)
- the endpoint-protection authorization gate
- `manual-review` classification
- an unsupported or unimplemented removal backend

See [Security model](security-model.md) for the full ordering.

## Using `force`

`force: true` suppresses only the final UI confirmation, and only for an action that
policy already authorized. It does not grant new eligibility. Use it exclusively with
centrally managed, access-controlled configuration deployment — anyone who can write
`policy.json` on the endpoint can act without a prompt.
