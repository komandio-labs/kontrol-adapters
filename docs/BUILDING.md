# Building

Install Python 3 and the .NET SDK required by the project, then build the repository from its root:

```powershell
dotnet build Kontrol.Adapters.slnx
dotnet test Kontrol.Adapters.slnx
```

The Dummy adapter, SDK, and sandbox build without a game installation. The Space Engineers 2 adapter additionally requires local game reference assemblies. Prepare them before building that project:

```powershell
python ./scripts/kontrol_adapters.py sync-se2
```

The script can use an explicit install location when Steam discovery is unavailable:

```powershell
python ./scripts/kontrol_adapters.py sync-se2 --game-directory '<SE2 installation>'
```

It writes only to the ignored `src/Adapters/SpaceEngineers2/references/<version>` directory. Do not add this directory, `bin`, `obj`, `.obj`, or `.bin` artifacts to source control.

## Local validation and packaging

Validate manifests, compatibility records, package rules, and portable tests:

```powershell
python ./scripts/kontrol_adapters.py validate
```

Run only one adapter's required validation:

```powershell
python ./scripts/kontrol_adapters.py test --adapter dummyadapter
python ./scripts/kontrol_adapters.py test --adapter spaceengineers2 --game-directory '<SE2 installation>'
```

The SE2 command prepares ignored local references, creates ignored inspection
evidence and a manual checklist, then runs SE2 automated tests. It never marks
a game build as tested automatically.

Create a local, unpublished package for one adapter:

```powershell
python ./scripts/kontrol_adapters.py pack --adapter dummyadapter --version 1.0.0
```

The package is written to ignored `artifacts/`. The command validates the
selected adapter, includes only its manifest allowlist, and rejects game DLLs,
PDBs, build output, logs, and undeclared files.
