# Space Engineers 2 adapter changelog

## Unreleased

## 0.1.0-beta.3 — validated beta

- Added target game build (`2.3.0.2798`) and verified core engine assemblies metadata directly to the adapter package manifest for automated local build verification in Kontrol.
- Validated the exact adapter package against Space Engineers 2 `2.3.0.2798`.
- Automated adapter validation passed (29 tests) and compatibility verification confirmed.
- Retains Native Plugin Parameter as the default deployment method and Process Injection as an alternate method.

## 0.1.0-beta.2 — validated beta

- Validated the exact adapter package against Space Engineers 2 `2.3.0.2798`.
- Automated adapter validation passed (29 tests) and the complete manual game checklist passed.
- Retains Native Plugin Parameter as the default deployment method and Process Injection as an alternate method.

## 0.1.0-beta.1 — first public beta

This beta is initially published as **Untested** until the exact package has
completed the automated and manual SE2 validation checklist.

- Native Plugin Parameter is now the default deployment method.
- Process Injection remains available as an alternate deployment method.

Historical baseline validation was performed against SE2 `2.3.0.2798` on
2026-07-31, but it does not constitute a Tested claim for this beta package.

- Declared the managed Process Injection entry point consumed by the Kontrol
  host's Steam-aware native injector.
- Removed the SE2 assembly-hook deployment path. Kontrol no longer rewrites
  `VRage.Library.dll`; use Process Injection or the native SE2 plugin loader.

## 1.0.0 — local release baseline

- Added versioned release manifest, compatibility record, and local inspection workflow.
- Separated the shared adapter runtime from the SE2 plugin and CoreCLR startup-hook entry points.

## 2026-07-30 — schema version 5

- Added Camera Mode Switch (`camera.mode_switch`) as trigger bit 13.
- Added Exit Grid, primary fire, and reload action support.
- Recorded historical Space Engineers 2 `2.3.0.2798` evidence; it must be revalidated before a Tested claim.
