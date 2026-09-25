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

### Version and SDK compatibility discipline

- A local build, pack, or repack is not a release and must not bump adapter versions or consume prerelease numbers. Before changing a version, check published tags/releases/catalog; never infer publication from local artifacts or commits. Change source versions only when the user requests a version/channel or authorizes release preparation.
- Classify SDK API changes before assigning a version: patch for fixes, minor for additive backward-compatible capabilities, major for removals or binary/wire incompatibility. Preserve legacy public members when introducing typed replacements in a minor release.
- Build and pack a new SDK locally, then prove the consuming app and affected adapters against that exact package. A local package is not published. Include the versioned SDK changelog in the package and use the same section for GitHub Release notes.
- Before publishing adapters that target a new SDK minor or major, release the compatible `kontrol-app` host first. Do not publish an adapter against an SDK version the public host does not support.
- Minor SDK versions must preserve compatibility with earlier minor versions of the same major. Any public API, binary layout, or IPC wire break requires a major version.

## Validate before packaging

Require a clean, reviewed source state. Run repository validation and the selected adapter's tests through `scripts/kontrol_adapters.py`. For game-dependent adapters, require local game references and completed game-specific validation. A fingerprint candidate or passing automated test alone must not create a `tested` claim.

Create and verify the package through the Python CLI. Ensure the package contains only manifest-allowlisted public runtime files and per-file checksums. Reject game DLLs, references, logs, dumps, PDBs, build directories, native object files, and undeclared files. All adapter packages follow `kontrol-adapter-<slug>-<version>-win-x64.zip`.

For every game adapter, run `python scripts/kontrol_adapters.py test --adapter
<slug>`, inspect the generated manual checklist, and do not mark a game build
`tested` until the user confirms completion of that exact in-game checklist.
During development, create a local Debug package when debug diagnostics need
in-game validation. Release candidates must be rebuilt, tested, and packaged in
Release; never publish a Debug package.

Before changing `src/Kontrol.Sdk/Versions.props`, query the configured package
registry for published versions. Keep an unpublished source version unless the
user requests a different version. For a selected adapter, keep its manifest,
README, changelog, compatibility records, and package metadata aligned,
including the latest verified game product version and relevant assembly
fingerprints.

Create an external release descriptor bound to the exact package SHA-256, adapter version, source tag, commit, architecture, and final release URL. Validate the descriptor against the package before publication.

## Guard external actions

Treat commit, tag, push, GitHub Release creation, asset upload, and catalog publication as external mutations. Perform them only when the user explicitly requests publication and the local release gates pass. Before acting:

1. Show the adapter, version, tag, commit, package path/hash, compatibility classification, and target repository.
2. Verify Git and `gh` authentication and that the intended commit is pushed.
3. Confirm the tag and release do not already exist.
4. Obtain user confirmation if any target or release metadata remains ambiguous.

Publish the locally built package and descriptor; GitHub-hosted workflows may verify artifacts and publish catalog metadata but must not build adapters requiring proprietary game references.

After publication, download or query the published assets, verify their hashes, and confirm catalog current/superseded status. Never delete or overwrite a release to repair it; issue a new adapter version or append-only compatibility revision.

After explicit publication authorization and successful gates, create the
annotated tag `adapters/<slug>/v<version>` from the reviewed commit merged into
`origin/main`, then let the GitHub Release workflow publish the immutable ZIP
and signed descriptor/catalog. Do not tag a feature-branch tip. Verify the
workflow, signed public package hash, release descriptor, catalog, and public
download URLs before reporting success. If Pages is unavailable, report the
exact failure and do not claim the adapter is downloadable.

Each adapter is an independent catalog identity. Match by the canonical
manifest `adapterId`/slug only; never use fuzzy game-name aliases or shared
fallbacks for release history, compatibility, update notices, or install
targets. Add cross-adapter regression tests when matching code changes.

### SDK publication gate

SDK publication is separate from local pack and compatibility proving. Never
publish or overwrite a package version unless the exact version is confirmed
absent from the authenticated package registry and the user explicitly
authorizes publication after the local SDK package, consuming host, and
affected adapters build and test successfully. Verify that SDK versions,
assembly metadata, `KontrolSdkContract.Version`, the `sdk/v<version>` tag, and
versioned changelog agree. Use the tag-triggered workflow and independently
verify the package and its GitHub Release notes. See `$kontrol-sdk-release` for
the complete SDK workflow.
