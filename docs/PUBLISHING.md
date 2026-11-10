# Publishing Kontrol adapters

This is the single release procedure for an adapter package. A published tag,
GitHub Release, ZIP, descriptor, and catalog entry are immutable.

## What gets published

The source repository may be private. The GitHub Release holds the locally
validated input ZIP. The release workflow publishes those exact ZIP bytes and
creates a signed descriptor and catalog on public GitHub Pages:

```text
catalog/v1/catalog.json
releases/<adapter>-<version>.json
packages/<adapter>/<version>/<package>.zip
```

Kontrol downloads the package from Pages, not from the GitHub Release. The
descriptor and catalog are cryptographically signed and bind the exact package
SHA-256. The ZIP is not changed after testing. Catalog installs authenticate
the package through the signed descriptor and its exact hash; an embedded
`signature.json` remains required for direct manual sideload outside that
authenticated catalog path.

## Before the first public release

1. Make the repository public.
2. Enable GitHub Pages with the `gh-pages` branch as the publishing source.
3. Enable GitHub private vulnerability reporting under **Settings → Advanced
   Security** and ensure maintainers receive security-alert notifications.
4. Confirm `KONTROL_SIGNING_PRIVATE_KEY` exists as a repository secret. Never
   print, download, or recreate this secret.

## Prepare one adapter release

1. Choose the adapter and version. Stable releases use normal SemVer such as
   `1.0.0`; beta releases use prerelease SemVer such as `0.1.0-beta.1`.
2. Update the adapter version properties, package manifest, runtime manifest,
   compatibility record, README, and changelog together.
3. Run the SE2 local test command and complete its manual checklist before
   claiming a release is Tested. An untested beta may still be published, but
   must say so in its metadata and notes.
4. Run:

   ```powershell
   python scripts/kontrol_adapters.py validate
   python scripts/kontrol_adapters.py test --adapter spaceengineers2
   dotnet build Kontrol.Adapters.slnx --no-restore
   dotnet test Kontrol.Adapters.slnx --no-restore
   ```

5. Commit the reviewed source change. Confirm the working tree is clean and
   that the scoped tag does not exist locally or remotely.
6. Build exactly one local **Release** package. A Debug package is never a
   publish candidate:

   ```powershell
   python scripts/kontrol_adapters.py pack --adapter spaceengineers2 --version 0.1.0 --configuration Release --output artifacts/kontrol-adapter-space-engineers-2-0.1.0-win-x64.zip
   python scripts/kontrol_adapters.py verify-package --package artifacts/kontrol-adapter-space-engineers-2-0.1.0-win-x64.zip
   Get-FileHash artifacts/kontrol-adapter-space-engineers-2-0.1.0-win-x64.zip -Algorithm SHA256
   ```

The package tool automatically includes the repository Apache-2.0 `LICENSE`.
Adapters that redistribute third-party runtime code must also declare and carry
their concrete third-party notice file in `package.json`.

## Publish after explicit approval

Create annotated tag `adapters/<slug>/v<version>`, push it, then create a
GitHub Release containing only the verified local ZIP. Mark beta releases as
prereleases. The `publish-adapter-pages.yml` workflow performs the signing and
Pages publication; do not manually create, sign, or upload a descriptor or
catalog.

After it succeeds, verify these anonymous HTTPS endpoints:

```text
https://komandio-labs.github.io/kontrol-adapters/catalog/v1/catalog.json
https://komandio-labs.github.io/kontrol-adapters/packages/<adapter>/<version>/<package>.zip
```

## AI-assisted release work

When working with Codex, use the repository skills as the preferred release
path:

- `kontrol-adapter-release` for validation, packaging, tags, releases, and
  post-publication checks.
- `kontrol-catalog-supply-chain` for package, descriptor, catalog, and
  signature-contract changes.
- `kontrol-github-release-engineering` for workflow, Pages, permissions, and
  repository-protection work.

The human-guided `scripts/publish_adapter.py` wizard is for an interactive
maintainer. AI agents must not invoke it; they follow the same checks directly
and wait for explicit authorization before external publication.

## Recover a release whose Pages workflow failed

Do not move its tag or replace the GitHub Release asset. After merging a
workflow fix to the default branch, rerun the publication workflow against that
branch and pass the existing adapter tag:

```powershell
gh workflow run publish-adapter-pages.yml --ref main `
  --field tag=adapters/<slug>/v<version> `
  --field announce=true
```

Set the `announce` input to `true` only when the corresponding Discord
announcement is authorized. The recovery run checks that the release is
published, verifies the downloaded ZIP against GitHub's asset digest and its
tested compatibility record, and refuses to replace an existing Pages
package or descriptor.

## Publish the Kontrol SDK

The SDK is a separate contract package published to the organization's GitHub
Packages NuGet feed. It is not published to NuGet.org. SDK releases use the
scoped tag sdk/v<version> and are handled by
.github/workflows/publish-sdk-nuget.yml.

Before tagging an SDK release:

1. Query GitHub Packages and confirm the exact Kontrol.Sdk version does not
   already exist. Published package versions are immutable.
2. Update src/Kontrol.Sdk/Versions.props and KontrolSdkContract.Version
   together, and add the matching section to
   src/Kontrol.Sdk/CHANGELOG.md.
3. Build and pack in Release. Confirm the generated .nupkg contains the SDK
   changelog and record its SHA-256.
4. Run the SDK/tooling tests, then build and test the consuming host and all
   affected adapters against the local SDK package.
5. Confirm the source commit is pushed and that sdk/v<version> and its GitHub
   Release do not already exist.

After explicit publication approval, push the annotated SDK tag. The workflow
validates the version and changelog, publishes the package, and creates the
GitHub Release from the matching changelog section. Verify both the package
version and release after the workflow completes. A manual workflow dispatch
must select an sdk/v<version> tag; branch-based SDK publication is rejected.
