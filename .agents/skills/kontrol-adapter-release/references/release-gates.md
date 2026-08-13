# Release gates

## Source

- Working tree contains only reviewed release changes.
- Adapter and SDK versions agree across properties, manifest, and assemblies.
- Changelog and adapter documentation describe the release.
- The release commit exists on the intended remote branch.

## Validation

- Repository metadata and portable tests pass.
- Selected adapter build/tests pass in normal output paths.
- Required local game fingerprints match the reviewed evidence.
- Manual in-game checklist passes before claiming `tested`.
- `git diff --check` passes and Git tracks no forbidden artifacts.

## Artifact

- Package filename, adapter version, architecture, and tag agree.
- Package verification and every per-file checksum pass.
- ZIP SHA-256 is recorded outside the ZIP in the release descriptor.
- Release descriptor commit, tag, URL, and package hash agree.

## Publication

- User explicitly authorized tagging/pushing/publishing.
- Tag and GitHub Release do not already exist.
- Release assets are immutable after publication.
- Catalog validation passes and marks exactly one current release per adapter.
- Compatibility-only publication reuses the existing package hash and does not rebuild the adapter.
