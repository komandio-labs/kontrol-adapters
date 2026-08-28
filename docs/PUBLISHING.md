# Publishing Kontrol adapters

This is the single release procedure for an adapter package. A published tag,
GitHub Release, ZIP, descriptor, and catalog entry are immutable.

## What gets published

The source repository may be private. The GitHub Release holds the locally
validated input ZIP. The release workflow then signs that ZIP and publishes only
these generated files to public GitHub Pages:

```text
catalog/v1/catalog.json
releases/<adapter>-<version>.json
packages/<adapter>/<version>/<package>.zip
```

Kontrol downloads the package from Pages, not from the GitHub Release. The
Pages package, descriptor, and catalog are all signed.

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
