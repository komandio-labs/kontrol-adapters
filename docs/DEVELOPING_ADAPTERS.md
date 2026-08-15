# Developing an adapter

1. Create an adapter folder under `src/Adapters/<GameName>`.
2. Put the adapter source project, its test project, `AdapterVersion.props`, `adapter.manifest.json`, compatibility records, and an adapter-specific `README.md` inside that folder.
3. Define an explicit input schema with stable IDs and append-only indices.
4. Translate only immutable SDK frames; keep game hooks and state local to the adapter.
5. Publish telemetry and logs through the SDK rather than writing normal adapter logs directly to disk.
6. Add unit tests for schema compatibility and input translation.
7. Document supported game versions, prerequisites, setup, diagnostics, and upgrade checks in the adapter README.
8. Declare target game build metadata in `package.json`: specify `gameProductVersion` (the verified game build) and `relevantAssemblies` (the engine/core game DLLs to inspect and fingerprint on disk). Keep the manifest limited to package identity, SDK contract, assembly, schema, platform, target game metadata, and package allowlist. Loading entry points remain in adapter code and tests.
9. Validate manifests and build a local package with the generic adapter tool before requesting a release.

When a target requires proprietary reference assemblies, provide a checked-in setup script that creates ignored local references. Do not commit or distribute game binaries.

Adapter and SDK semantic-version sources are validated against the manifest and
assembly metadata. See [VERSIONING.md](VERSIONING.md) for policy and
[PUBLISHING.md](PUBLISHING.md) for the release procedure.
