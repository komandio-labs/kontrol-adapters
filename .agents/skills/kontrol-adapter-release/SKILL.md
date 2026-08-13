---
name: kontrol-adapter-release
description: Prepare, validate, version, package, tag, publish, or audit Kontrol adapter releases and compatibility metadata. Use for adapter or SDK version changes, deterministic ZIPs, release descriptors, Git tags, GitHub Releases, catalog generation, compatibility-only updates, and current-versus-superseded release status. Do not use for ordinary adapter implementation or debugging.
---

# Kontrol Adapter Release

## Read the release contract

Read applicable `AGENTS.md`, `docs/VERSIONING.md`, `docs/BUILDING.md`, the adapter manifest, `AdapterVersion.props`, changelog, compatibility records, and [references/release-gates.md](references/release-gates.md). Inspect Git status and remotes before proposing release operations.

## Classify the change

- Publish a new adapter version when code, dependencies, packaging, schema, or runtime behavior changes.
- Publish compatibility metadata only when the exact existing adapter package is newly validated against another game build without binary changes.
- Preserve old tags and artifacts as immutable history. Never patch or replace a published artifact.
- Maintain only the newest adapter release; do not backport fixes to older releases.

Confirm semantic-version impact and keep `AdapterVersion.props`, manifest, assembly metadata, tag, package filename, and release descriptor consistent. Keep SDK API and IPC under the single SDK contract version.

## Validate before packaging

Require a clean, reviewed source state. Run repository validation and the selected adapter's tests through `scripts/kontrol_adapters.py`. For game-dependent adapters, require local game references and completed game-specific validation. A fingerprint candidate or passing automated test alone must not create a `tested` claim.

Create and verify the package through the Python CLI. Ensure the package contains only manifest-allowlisted public runtime files and per-file checksums. Reject game DLLs, references, logs, dumps, PDBs, build directories, native object files, and undeclared files.

Create an external release descriptor bound to the exact package SHA-256, adapter version, source tag, commit, architecture, and final release URL. Validate the descriptor against the package before publication.

## Guard external actions

Treat commit, tag, push, GitHub Release creation, asset upload, and catalog publication as external mutations. Perform them only when the user explicitly requests publication and the local release gates pass. Before acting:

1. Show the adapter, version, tag, commit, package path/hash, compatibility classification, and target repository.
2. Verify Git and `gh` authentication and that the intended commit is pushed.
3. Confirm the tag and release do not already exist.
4. Obtain user confirmation if any target or release metadata remains ambiguous.

Publish the locally built package and descriptor; GitHub-hosted workflows may verify artifacts and publish catalog metadata but must not build adapters requiring proprietary game references.

After publication, download or query the published assets, verify their hashes, and confirm catalog current/superseded status. Never delete or overwrite a release to repair it; issue a new adapter version or append-only compatibility revision.
