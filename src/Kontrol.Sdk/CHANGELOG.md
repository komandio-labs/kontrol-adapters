# Kontrol SDK Changelog

All notable changes to the Kontrol SDK contract and package are documented
here. The SDK version covers both the adapter API and the IPC contract.

## [1.3.0] - 2026-09-08

### Added

- Added `AdapterTraceDescriptor` and adapter-owned `SupportedTraces` metadata
  for optional diagnostic traces.

### Fixed

- Updated `KontrolSdkContract.Version` to report `1.3.0`, matching the package
  and assembly version.

## [1.2.0] - 2026-08-27

### Added

- Added cross-kind input metadata for axis, button, and button-pair sources.
- Added explicit discrete state/event delivery metadata, direction labels, and
  axis threshold defaults.
- Added typed measurement units and adapter-resolved numeric presentation
  metadata, including runtime presentation variants.
- Added adapter settings schema revision metadata and telemetry frames that can
  carry numeric presentation updates.

### Compatibility

- Preserved the SDK 1.1.x `InputDescriptor` and
  `AdapterSettingsSnapshot` constructor signatures for already-installed
  adapters.

### Historical note

- The `KontrolSdkContract.Version` constant in the 1.2.0 source tag remained
  `1.1.0`; this was corrected in 1.3.0.

## [1.1.1] - 2026-08-24

### Changed

- Bumped the SDK package and assembly version to `1.1.1` to align the package
  and first-party adapter manifests.

### Historical note

- This release contains no SDK API or implementation changes relative to
  1.1.0. The contract constant remained `1.1.0`.

## [1.1.0] - 2026-08-24

### Added

- Added declarative adapter settings descriptors for booleans, numbers,
  strings, arrays, options, conditions, icons, and layout metadata.
- Added live adapter-settings IPC support and settings snapshots.
- Added adapter settings-provider and deployment metadata capabilities.
- Expanded adapter connection and log diagnostics reporting.

## [1.0.0] - 2026-07-31

### Added

- Initial published Kontrol SDK package containing the adapter interfaces,
  input schema, IPC structures, deployment metadata, and diagnostics reporters.

### Historical note

- The current repository does not contain a formal `sdk/v1.0.0` source tag, so
  the exact originating commit for this package is not preserved as a scoped
  SDK release tag.

[1.3.0]: https://github.com/komandio-labs/kontrol-adapters/releases/tag/sdk/v1.3.0
[1.2.0]: https://github.com/komandio-labs/kontrol-adapters/releases/tag/sdk/v1.2.0
[1.1.1]: https://github.com/komandio-labs/kontrol-adapters/releases/tag/sdk/v1.1.1
[1.1.0]: https://github.com/komandio-labs/kontrol-adapters/releases/tag/sdk/v1.1.0
