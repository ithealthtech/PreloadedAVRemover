# Catalog

`Catalog/oem-removal-catalog.json` is the reviewable source of truth for what the tool
recognizes. It is embedded into the executable at build time. The catalog currently holds
82 entries.

The catalog describes software. It does **not** grant permission to remove it - policy,
the authorization gates, backend support, elevation, and command validation are all
evaluated separately. See [Security model](security-model.md).

## Entry format

```json
{
  "id": "security-mcafee-msi",
  "vendor": "McAfee",
  "brand": "Any",
  "productPattern": "(^|\\W)(McAfee|WebAdvisor)(\\W|$)",
  "detectionMethod": "MSI uninstall registry",
  "packageType": "Msi",
  "riskLevel": "Caution",
  "silentUninstallTemplate": "msiexec.exe /x {ProductCode} /qn /norestart",
  "knownDependencies": [],
  "rebootRequired": true,
  "isSecurityProduct": true,
  "notes": "Common OEM security trial; removal requires explicit security-product authorization."
}
```

## Field reference

| Field | Required | Meaning |
| --- | --- | --- |
| `id` | yes | Stable unique identifier. Duplicates are rejected at load. |
| `vendor` | yes | Publisher name, used as matching evidence. |
| `brand` | yes | Hardware brand this applies to, or `Any`. Used as evidence. |
| `productPattern` | yes | Regular expression matched against the product name. Malformed expressions are rejected at load. |
| `detectionMethod` | yes | Which inventory source surfaces the item. |
| `packageType` | yes | Which removal backend would apply. |
| `riskLevel` | yes | `Safe`, `Caution`, or `ManualReview`. |
| `knownDependencies` | yes | Software that depends on this item. |
| `rebootRequired` | yes | Whether removal typically requires a reboot. |
| `notes` | yes | Operator-facing rationale, shown in reports. |
| `automaticRemovalSupported` | no | Present on 34 entries. Absent means unsupported. |
| `isSecurityProduct` | no | Marks endpoint protection, routing the entry behind the security gate. |
| `silentUninstallTemplate` | no | Template for backends that rebuild the command rather than reuse the vendor string. |

## Accepted values

**`brand`** — `Any`, `Acer`, `Alienware`, `ASUS`, `Dell`, `Dynabook`, `Fujitsu`,
`Gigabyte`, `HP`, `Lenovo`, `LG`, `Microsoft`, `MSI`, `Razer`, `Samsung`

**`packageType`** — `Msi`, `Exe`, `Appx`, `Winget`, `Service`, `ScheduledTask`,
`RegistryEntry`

**`detectionMethod`** — `Uninstall registry`, `MSI uninstall registry`, `AppX metadata`,
`winget package metadata`, `Win32_Service`, `ScheduledTasks metadata`,
`Explicit registry catalog`

**`riskLevel`** — see the profile table in [Configuration](configuration.md):

| Value | Automatic eligibility |
| --- | --- |
| `Safe` | Eligible under all three profiles |
| `Caution` | Eligible under `Balanced` and `Aggressive`, with confirmation |
| `ManualReview` | Never automatically eligible under any profile |

## Backend support

`Service`, `ScheduledTask`, and `RegistryEntry` entries are **audit and manual-review
only**. They are detected and reported, but they fail closed rather than executing,
because no dedicated tested handler exists for them. Adding one of these package types to
the catalog does not make the item removable; it makes it visible.

## Proposing an entry

Open a catalog request issue with:

1. The exact `DisplayName` and `Publisher` from the uninstall registry key.
2. The OEM brand and a specific hardware model where you observed it.
3. Which category it belongs to and why it is safe to remove.
4. The vendor uninstall string, if one exists.
5. Anything that depends on it.

Then, if you are opening a pull request:

- Choose the most conservative `riskLevel` that is defensible. `ManualReview` is a
  perfectly good answer for anything uncertain.
- Write `productPattern` to be as specific as the evidence allows. A pattern that also
  matches a driver, control panel, or firmware utility is a defect, not a convenience.
- Add a regression test asserting the expected match and the expected policy decision.
  See [Testing](testing.md).
- Do not set `automaticRemovalSupported` unless the removal has actually been tested on
  real hardware.

Catalog changes should be hardware-ring tested before broad aggressive deployment. OEM
product names, installer technology, and supported silent flags vary by model and
release.
