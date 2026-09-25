---
name: kontrol-sdk-release
description: Prepare, validate, publish, or troubleshoot a Kontrol.Sdk GitHub Packages release with version-specific changelog and GitHub Release notes.
---

# Kontrol SDK Release

Use for requests such as "publish the SDK" or "release Kontrol.Sdk". Work in
`kontrol-adapters` and read its versioning/publishing docs first.

1. Query GitHub Packages, tags, and releases for existing SDK versions.
2. Propose the exact next version and inspect all commits since the previous tag.
3. Update `src/Kontrol.Sdk/CHANGELOG.md` with only that version's changes.
4. Build, pack, hash, and inspect the `.nupkg`; prove the consuming app and
   affected adapters against that exact local package.
5. Show the final changelog and ask for release approval before mutation.
6. Commit and push merged `main`, then create `sdk/v<version>`.
7. Let the tag workflow publish GitHub Packages and create the GitHub Release.
8. Verify the exact package version, package contents, release body, release
   asset, and workflow result independently.

The GitHub Release body must contain the same version-specific changelog
section as the package. Never confuse package description metadata with release
notes. Never overwrite an existing package version or tag.
