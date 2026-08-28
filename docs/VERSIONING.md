# Kontrol adapter and game versioning plan

This document defines how the public `kontrol-adapters` repository versions,
validates, publishes, and maintains adapter source and release artifacts. It is
the authoritative maintenance policy for this repository.

## Implementation status

The repository currently implements local validation, deterministic local
packaging, manifests, compatibility records, immutable release descriptors,
catalog generation, signed-package verification, and generic test selection.
Game-specific validation is intentionally run by a developer on a local Windows
machine with locally prepared game references. The GitHub release workflow signs
the release package and publishes the signed package, descriptor, and catalog to
GitHub Pages. Kontrol consumes that signed public catalog. See
[PUBLISHING.md](PUBLISHING.md) for the only supported release procedure.

## Goals

- Preserve the exact source and artifacts for every published adapter release.
- Record exactly which game builds were tested with each adapter release.
- Publish compatibility metadata that distinguishes tested, untested, and known-incompatible game builds.
- Publish every adapter as its own independently versioned release.
- Never publish proprietary game assemblies or local build artifacts.
- Keep the Kontrol SDK API and IPC contract under one version.

## Versioned components

The following versions have distinct purposes:

| Component | Example | Meaning |
| --- | --- | --- |
| Kontrol SDK contract | `1.2.0` | Adapter API and IPC contract together. |
| Adapter | `1.3.1` | Independently published implementation for one target. |
| Game build | `2.3.0.2798` plus fingerprints | Installed game code against which the adapter was validated. |

There is no separate IPC protocol version. The IPC structures are part of the
Kontrol SDK contract and use the SDK contract version.

### SDK SemVer & Backward Compatibility Invariant

- **Minor Version Invariant (Backward Compatibility)**: Every minor SDK bump (e.g. `1.0.0` -> `1.1.0` -> `1.2.0`) **must preserve 100% backward compatibility** with all previous minor versions of the same major version. An updated Kontrol host application must continue to support adapters compiled against earlier minor versions.
- **Breaking Changes Require Major Version**: Any change that modifies or removes existing public APIs, interfaces, binary layouts, or IPC wire protocol contracts is strictly a **MAJOR** version bump (e.g. `2.0.0`). Breaking changes in a minor SDK release are strictly prohibited.

## Source-code versioning

Each adapter has one current source tree on `main`. Do not create copied source
directories such as `Versions/1.0.0` or `Versions/1.1.0`.

```text
src/Adapters/SpaceEngineers2/
  README.md
  CHANGELOG.md
  adapter.manifest.json
  compatibility/
  scripts/
  Kontrol.Adapters.SpaceEngineers2/
  Kontrol.Adapters.SpaceEngineers2.Tests/
```

Git tags preserve the exact repository source used for each release. Tags are
scoped by adapter because adapters are released independently:

```text
adapters/spaceengineers2/v1.0.0
adapters/spaceengineers2/v1.1.0
adapters/dummyadapter/v1.0.0
```

Tags and published release artifacts are immutable. Never move a release tag or
replace an existing release artifact. Any adapter package change requires a new
adapter version; compatibility-only evidence uses a new signed attestation.

Adapter maintenance is forward-only. Publishing a new adapter version
supersedes every previous version of that adapter. Previous tags and artifacts
remain available only for source history, audit, and reproducibility; they do
not receive bug fixes, backports, or compatibility updates. Every fix is made on
the current source and published as a newer adapter release.

## Adapter semantic versioning

Adapter versions follow semantic versioning:

- **Major**: breaking saved-mapping/schema change, removed or reordered input,
  incompatible packaging change, or other change requiring user migration.
- **Minor**: backward-compatible input/action addition, support for a new game
  API family, new telemetry, or another substantial compatible feature.
- **Patch**: implementation, packaging, or diagnostics fix that changes the
  published adapter package without introducing a breaking contract.

Existing input IDs and schema indices are append-only. Removing, renaming,
reordering, or reusing an index is a breaking adapter change.

Validating an unchanged adapter package against another game build does not
require a new adapter version. Publish a new signed compatibility attestation
bound to the exact existing package hash. Publish a new adapter release only
when code, dependencies, packaging, schema, or runtime behavior changes.

The newest adapter release defines the complete active game-support policy. It
does not inherit support promises from older adapter releases. When a new
adapter targets a newer game build, users of the active release are expected to
update the game to that validated build.

## Kontrol SDK contract versioning

The SDK API and IPC structures share one semantic version and one source of
truth. The SDK assembly version, package version, adapter manifest, and IPC
header derive from that value.

- **Major**: breaking API or IPC layout/semantic change.
- **Minor**: backward-compatible API or IPC capability addition.
- **Patch**: implementation fix with no contract change.

Adapters declare the SDK version against which they were built. Exact version
equality is not required: compatible minor and patch versions within the same
major may communicate when structure sizes and capabilities permit it.

A game-version mismatch is advisory. An incompatible SDK major is a hard error
because the host and adapter cannot safely interpret the same contract.

## Adapter release manifest

Every adapter source tree contains `adapter.manifest.json`. The publishing
workflow validates it and includes an immutable copy in the release package.

Required information includes:

```json
{
  "manifestVersion": 1,
  "adapterId": "SpaceEngineers2",
  "adapterVersion": "1.1.0",
  "sdkVersion": "1.0.0",
  "entryAssembly": "Kontrol.Adapters.SpaceEngineers2.dll",
  "inputSchemaVersion": 5,
  "targetFramework": "net9.0"
}
```

The release manifest identifies the adapter package contents, but it cannot
contain the SHA-256 of the final ZIP that contains it. The package carries
per-file checksums; an external release descriptor and catalog carry the final
package SHA-256. The generic `release create` command binds the ZIP hash to its
immutable source tag, commit, and intended release URL. Tested-game claims are published separately so
compatibility can be extended without changing or redownloading an unchanged
adapter binary.

## Signed compatibility attestations

A compatibility attestation binds one exact published adapter package to one
tested game build:

```json
{
  "schemaVersion": 1,
  "catalogRevision": 12,
  "adapterId": "SpaceEngineers2",
  "adapterVersion": "1.1.0",
  "adapterPackageSha256": "ABC123...",
  "gameBuild": {
    "productVersion": "2.3.1.1000",
    "steamBuildId": "12345678",
    "relevantAssemblies": {
      "Game2.Client.dll": {
        "sha256": "DEF456...",
        "mvid": "..."
      }
    }
  },
  "validationDate": "2026-08-10",
  "result": "tested",
  "signature": "..."
}
```

Compatibility records are append-only, signed, and preserved through Git
history or compatibility-scoped tags. When more than one adapter
version is validated against the same game build, keep one record per adapter
version and use the filename form
`<game-product-version>-adapter-<adapter-version>.json`:

```text
compatibility/spaceengineers2/r12
```

Never edit the manifest inside an existing package or replace an existing
attestation. Publish a new catalog revision instead.

## Tested game builds

The game product version is the primary human-facing compatibility identifier.
Compatibility is based on explicit validation records, not broad numeric
version ranges. Private game APIs may break in a patch or repackaged build
without a useful semantic-version indication.

A tested record should include:

- Game product/file version.
- SHA-256 hashes for only the assemblies whose private types, methods, fields,
  or method bodies the adapter uses.
- MVIDs for those same relevant assemblies.
- Validation date.
- Adapter version used during validation.
- Exact adapter package SHA-256 used during validation.
- Manual validation result and any known limitations.

Do not fingerprint every game DLL. Each adapter declares the small set of files
that can affect its integration. Full hashes are a conservative first
implementation; an adapter-specific API/symbol fingerprint may replace them
later to avoid warnings for unrelated binary changes.

Matching product version and relevant fingerprints is **Tested exact build**. A
matching product version with different relevant fingerprints is **Tested
version, different build** and remains untested until reviewed. A different
product version with unchanged relevant fingerprints is strong compatibility
evidence, but it still requires a signed validation attestation before becoming
an official Tested claim.

Historical game validation remains recorded in immutable release manifests and
tags for audit purposes. It is not an active support commitment after that
adapter release has been superseded.

## Published compatibility classifications

Compatibility attestations and the public catalog use these classifications:

| State | Repository meaning |
| --- | --- |
| Tested | A signed record binds the exact adapter package to the validated game version and relevant fingerprint. |
| Untested | No exact tested record exists; compatibility is neither promised nor rejected. |
| Known incompatible | A documented breaking issue exists for the game build. |
| Unknown | Available metadata is insufficient to classify the game build. |

The public metadata must never describe an untested build as supported. Adapter
READMEs and release notes must state that untested use is not guaranteed and is
at the user's own risk. How a catalog consumer presents or acknowledges that
risk is outside the scope of this public-repository plan.

## Public adapter catalog

Publishing workflows generate a versioned public catalog from immutable GitHub
Releases. The catalog retains metadata for current and superseded releases so a
consumer can relate an installed game build to the adapter release tested with
it. Catalog consumers use published entries rather than inferring versions from
repository branches or source directories.

```text
https://komandio-labs.github.io/kontrol-adapters/catalog/v1/catalog.json
```

Each catalog entry includes:

- Adapter ID and adapter version.
- SDK contract version.
- Signed tested-game compatibility attestations.
- Known incompatible builds.
- Git tag and source URL.
- Release package URL.
- Package SHA-256 and cryptographic signature.
- Publication time and release channel.
- Release status: `current` or `superseded`.

### Release channels

Every release descriptor declares a publication channel: `stable` or `beta`.
Stable releases use a normal semantic version such as `1.2.0`; beta releases
must use a prerelease version such as `1.3.0-beta.1`. The publishing tool
rejects a prerelease labelled stable or a normal version labelled beta.

The catalog retains beta descriptors, but its `currentVersion` selects the
newest stable release when one exists. Consumers must show and select stable
releases by default, and require explicit user opt-in before offering a beta
package for installation. This channel is not a compatibility claim: beta
packages still require their own signed package and exact compatibility data.

The catalog schema is versioned independently as a document format. Its version
does not describe SDK or adapter compatibility.

## Adapter matching metadata

The catalog provides enough metadata to evaluate adapter releases in this
order:

1. Compatible Kontrol SDK major.
2. Exact tested game fingerprint.
3. Exact tested product version, classified as untested if fingerprints differ.
4. Highest adapter version among otherwise equal exact matches.

A catalog entry must not claim compatibility merely because a game version is
numerically close to a tested version.

Superseded releases remain downloadable and identifiable but are not maintained,
patched, or actively supported. Their presence allows a catalog consumer to
avoid replacing a working adapter merely because a newer adapter was published
for a newer game build. For an older game build, the supported path is to update
the game and then use the current adapter release.

Publishing a new adapter release alone is not evidence that an existing adapter
installation should be replaced. The installed game build and tested-build
metadata determine whether a newer adapter is relevant.

## Publication workflow

An adapter release is initiated by an immutable scoped tag and GitHub Release.
For example:

```text
adapters/spaceengineers2/v1.2.0
```

The release workflow:

1. Parse the adapter ID and version from the tag.
2. Confirm that the manifest version matches the tag.
3. Confirm that the tag does not already have a published artifact.
4. downloads the already locally validated ZIP from the private GitHub Release.
5. signs and verifies that ZIP with the repository signing secret.
6. generates a descriptor bound to the signed package hash and source tag.
7. marks the previous catalog entry for that adapter as superseded without
    modifying its immutable release artifacts.
8. regenerates, validates, signs, and publishes the public adapter catalog with the
    new current release and historical superseded metadata.

A release failure publishes neither a partial adapter release nor a catalog
entry.

## Compatibility publication workflow

When a game build is validated with an unchanged adapter package, a protected
publication workflow must:

1. Select an existing immutable adapter release.
2. Download and verify its exact published package and SHA-256.
3. Verify the local game version and relevant assembly fingerprints.
4. Run automated compatibility tests against that exact adapter binary.
5. Require the adapter's manual validation checklist.
6. Generate and sign a new append-only compatibility attestation.
7. Publish a new signed catalog revision without rebuilding the adapter DLL.

This workflow publishes metadata only. It must not mutate the adapter release,
replace its manifest, or distribute game-owned assemblies.

## Proprietary game references and CI

Game-owned reference assemblies remain in ignored local version directories and
are never uploaded as source, cache, workflow artifact, or release content.

Adapters that compile against proprietary assemblies require a protected,
self-hosted Windows release runner with locally prepared references, unless the
adapter is later changed to avoid compile-time game dependencies.

The release runner keeps references outside the Git checkout, organized by game
version. Before compiling, it verifies their hashes against the adapter
manifest.

Public fork pull requests must never execute automatically on a self-hosted
runner. Public validation can build the SDK, Dummy Adapter, sandbox, and other
code without proprietary dependencies on GitHub-hosted runners. Game-specific
release builds run only from reviewed code through protected tags or manually
approved environments.

## Game update maintenance procedure

When a game update is published:

1. Run the adapter reference synchronization/inspection script against the new
   local installation.
2. Record the product version, hashes, and MVIDs without committing DLLs.
3. Compare required symbols, signatures, fields, enum values, and behavior with
   the current compatibility documentation.
4. Build and run automated tests using the new local reference set.
5. Perform the adapter's manual in-game validation checklist.
6. If the existing adapter package works unchanged, publish a signed
   compatibility attestation and catalog revision without an adapter release.
7. If code changes are required, adapt the current source and release a minor,
   patch, or major version according to the compatibility impact.
8. If the build is known to fail, document it as known incompatible and publish
   that information through a new release/catalog update.
9. Update the adapter README and changelog.

Until validation is complete, the public metadata classifies the new game build
as untested. After a newer active adapter release is published, older adapter
versions are no longer maintained and older game builds are not recommended.

## SDK update maintenance procedure

When the Kontrol SDK changes:

1. Classify the change as major, minor, or patch.
2. Update the single SDK contract version.
3. Keep additive IPC changes size-aware and capability-driven.
4. Update first-party adapters and tests.
5. Publish the SDK before publishing adapters that require it.
6. Declare the new SDK version in each affected adapter manifest.
7. Publish a new active adapter release; never patch or replace old artifacts.

The catalog records each adapter release's SDK version so consumers can exclude
incompatible SDK major versions.

## Release checklist

Before tagging an adapter release, confirm:

- Adapter manifest and tag versions match.
- SDK requirements are accurate.
- Any bundled baseline compatibility evidence is exact and reviewed.
- README and changelog are updated.
- The normal output build succeeds with zero errors.
- All relevant tests pass.
- Manual game validation is recorded where required.
- Package contents contain no game-owned DLLs or build artifacts.
- Download hash and signature verification succeed.
- The catalog exposes this release as current and preserves older entries as
  superseded history.
- Untested builds are not represented as supported or guaranteed.

Before publishing compatibility metadata without an adapter release, confirm:

- The attestation references the exact published adapter package SHA-256.
- Product version and relevant assembly fingerprints are reviewed.
- Automated and manual validation used that exact adapter package.
- The attestation and catalog revision signatures verify.
- No adapter package, manifest, tag, or game-owned assembly was modified.

## Decisions intentionally avoided

- No copied source-code directory per adapter release.
- No mutable release tags or overwritten artifacts.
- No broad game-version range treated as proof of compatibility.
- No proprietary game DLLs in Git, caches, or releases.
- No maintenance branches or bug-fix backports for superseded adapter releases.
- No assumption that a newer adapter release is relevant before the installed
  game build changes.
