---
name: kontrol-adapter-release
description: Prepare, validate, version, package, tag, publish, or audit Kontrol adapter releases and compatibility metadata. Use for adapter or SDK version changes, deterministic ZIPs, release descriptors, Git tags, GitHub Releases, catalog generation, compatibility-only updates, and current-versus-superseded release status. Do not use for ordinary adapter implementation or debugging.
---

# Kontrol Adapter Release

## Read the release contract

Read applicable `AGENTS.md`, `docs/VERSIONING.md`, `docs/BUILDING.md`, the adapter manifest, `AdapterVersion.props`, changelog, compatibility records, and [references/release-gates.md](references/release-gates.md). Inspect Git status and remotes before proposing release operations.

## Write player-facing release copy

Write changelog entries and release notes for players. Include only concise,
plain-language outcomes they can notice or use, and compatibility details
that are verified for the exact game build. Do not include implementation
internals, SDK or schema metadata, assembly fingerprints, test counts, CI
results, manual checklist status, or other engineering evidence. Keep test and
manual-validation evidence in compatibility records, checklists, and issue
tracking. If a change has no verified player-visible outcome, omit it from
public release copy. Express host compatibility as the required Kontrol app
version, rather than the internal SDK version.

## Classify the change

- Publish a new adapter version when code, dependencies, packaging, schema, or runtime behavior changes.
- Publish compatibility metadata only when the exact existing adapter package is newly validated against another game build without binary changes.
- Preserve old tags and artifacts as immutable history. Never patch or replace a published artifact.
- Maintain only the newest adapter release; do not backport fixes to older releases.

Confirm semantic-version impact and keep `AdapterVersion.props`, manifest, assembly metadata, tag, package filename, and release descriptor consistent. Keep SDK API and IPC under the single SDK contract version.

## Compatibility-only publication (no adapter version bump)

Use this path when an unchanged published adapter package is validated against
a new game build. The catalog-served Pages ZIP is the package users install;
the GitHub Release asset and Pages ZIP may have different hashes on older
releases if a workflow rewrote the archive to add `signature.json`.

1. Resolve the exact current release by canonical adapter ID/slug. Fetch the
   signed Pages descriptor and download the package from its `package.url`.
   Separately query the GitHub Release asset digest. Hash both ZIPs and compare
   each value with its matching descriptor/asset.
2. If the Release asset and Pages ZIP hashes differ, do not treat the files as
   interchangeable, even if their entries appear identical or differ only by
   `signature.json`. The Pages ZIP is the compatibility-test candidate because
   it is the artifact installed from the catalog. Stop until the user confirms
   manual testing of that exact Pages ZIP and game build.
3. Run automated tests against local references for the new game build, then
   require the user's manual checklist confirmation for the exact Pages ZIP.
   Record the existing adapter version, build identity, relevant fingerprints,
   automated/manual results, and `adapterPackageSha256` equal to the Pages
   descriptor's package SHA-256. Keep the attestation append-only.
4. Update the adapter's source `package.json`, README, and changelog for the
   latest verified build without changing the already-published ZIP, adapter
   version, release tag, or immutable descriptor in Pages.
5. Validate the repository, adapter, compatibility evidence, and exact package.
   Before opening the PR, run `release update-descriptors` against a local copy
   of the current Pages descriptor. Confirm `verifiedGameVersions`, the current
   game identity/fingerprints, and the unchanged `package.sha256` are correct.
   This catches compatibility record fields the refresh tool may not consume.
   The `refresh-catalog.yml` workflow runs the same update, signs the result,
   and must be checked after publication.
6. Merge the reviewed PR, then dispatch `refresh-catalog.yml` from `main` with
   a reason. Do not dispatch `publish-adapter-pages.yml` for an existing
   descriptor; that workflow is for first publication/recovery and refuses to
   replace immutable Pages files.
7. Verify the workflow succeeded, the signed descriptor includes the new
   tested game version, the signed catalog remains valid/current, and the
   public Pages ZIP hash is unchanged. Do not repack or replace the adapter ZIP.

### Version and SDK compatibility discipline

- A local build, pack, or repack is not a release and must not bump adapter versions or consume prerelease numbers. Before changing a version, check published tags/releases/catalog; never infer publication from local artifacts or commits. Change source versions only when the user requests a version/channel or authorizes release preparation.
- Classify SDK API changes before assigning a version: patch for fixes, minor for additive backward-compatible capabilities, major for removals or binary/wire incompatibility. Preserve legacy public members when introducing typed replacements in a minor release.
- Build and pack a new SDK locally, then prove the consuming app and affected adapters against that exact package. A local package is not published. Include the versioned SDK changelog in the package and use the same section for GitHub Release notes.
- Before publishing adapters that target a new SDK minor or major, release the compatible `kontrol-app` host first. Do not publish an adapter against an SDK version the public host does not support.
- Minor SDK versions must preserve compatibility with earlier minor versions of the same major. Any public API, binary layout, or IPC wire break requires a major version.

## Validate before packaging

Require a clean, reviewed source state. Run repository validation and the selected adapter's tests through `scripts/kontrol_adapters.py`. For game-dependent adapters, require local game references and completed game-specific validation. A fingerprint candidate or passing automated test alone must not create a `tested` claim.

Create and verify the package through the Python CLI. Ensure the package contains only manifest-allowlisted public runtime files and per-file checksums. Reject game DLLs, references, logs, dumps, PDBs, build directories, native object files, and undeclared files. All adapter packages follow `kontrol-adapter-<slug>-<version>-win-x64.zip`.

Building or packaging an adapter into repository-owned build/artifact directories does not require stopping Kontrol or the game when those outputs are separate from deployed runtime files. Before installing, copying over, replacing, or deleting a deployed DLL or other runtime file, check whether the game or Kontrol process may have that exact target loaded; ask the user to close only the affected process if the target is locked. Do not use process checks as a blanket prerequisite for adapter builds or packages.

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

Create an external release descriptor bound to the exact package SHA-256, adapter version, source tag, commit, architecture, and final release URL. Validate the descriptor against the package before publication. The publication workflow must preserve the validated ZIP bytes; do not append or rewrite `signature.json` after the package was tested. Sign the descriptor and catalog, which bind and authenticate the exact ZIP hash. Validate an embedded package signature if one is already present.

## Guard external actions

Treat commit, tag, push, GitHub Release creation, asset upload, and catalog publication as external mutations. Perform them only when the user explicitly requests publication and the local release gates pass. Before acting:

1. Show the adapter, version, tag, commit, package path/hash, compatibility classification, and target repository.
2. Verify Git and `gh` authentication and that the intended commit is pushed.
3. Confirm the tag and release do not already exist.
4. Obtain user confirmation if any target or release metadata remains ambiguous.

Publish the locally built package and descriptor; GitHub-hosted workflows may verify artifacts and publish catalog metadata but must not build adapters requiring proprietary game references.

After publication, download or query the published assets, verify that the GitHub Release ZIP and Pages ZIP have the same hash as the tested package, verify the signed descriptor/catalog, and confirm catalog current/superseded status. Never delete or overwrite a release to repair it. A failed Pages publication may be recovered through the supported workflow dispatch for the existing tag when no Pages descriptor exists; never move the immutable tag or replace its release asset.

After explicit publication authorization and successful gates, create the
annotated tag `adapters/<slug>/v<version>` from the reviewed commit merged into
`origin/main`, then let the GitHub Release workflow publish the immutable ZIP
and signed descriptor/catalog. Do not tag a feature-branch tip. Verify the
workflow, unchanged public package hash, signed release descriptor, signed
catalog, and public
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
