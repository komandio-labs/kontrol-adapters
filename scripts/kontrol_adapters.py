#!/usr/bin/env python3
"""Cross-platform developer and release command for kontrol-adapters."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
TOOL_PROJECT = ROOT / "tools" / "Kontrol.AdapterTool" / "Kontrol.AdapterTool.csproj"
ADAPTERS = {
    "dummy-adapter": ("DummyAdapter", "Kontrol.Adapters.DummyAdapter"),
    "dummyadapter": ("DummyAdapter", "Kontrol.Adapters.DummyAdapter"),
    "space-engineers-2": ("SpaceEngineers2", "Kontrol.Adapters.SpaceEngineers2"),
    "spaceengineers2": ("SpaceEngineers2", "Kontrol.Adapters.SpaceEngineers2"),
}
SE2_ASSEMBLIES = (
    "Game2.Client.dll", "Game2.Simulation.dll", "VRage.Core.dll", "VRage.Core.Game.dll",
    "VRage.DCS.dll", "VRage.Library.dll", "VRage.Physics.dll", "VRage.Input.dll",
)


def run(*command: str, capture: bool = False) -> str:
    result = subprocess.run(command, cwd=ROOT, check=False, text=True, capture_output=capture)
    if result.returncode:
        if capture:
            sys.stderr.write(result.stderr)
        raise RuntimeError(f"Command failed ({result.returncode}): {' '.join(command)}")
    return result.stdout if capture else ""


def tool(*arguments: str, capture: bool = False) -> str:
    return run("dotnet", "run", "--project", str(TOOL_PROJECT), "--", *arguments, capture=capture)


def adapter_paths(slug: str) -> tuple[Path, Path, Path]:
    try:
        folder, assembly_name = ADAPTERS[slug]
    except KeyError as error:
        raise RuntimeError(f"Unknown adapter '{slug}'.") from error
    root = ROOT / "src" / "Adapters" / folder
    return root, root / assembly_name / f"{assembly_name}.csproj", root / f"{assembly_name}.Tests" / f"{assembly_name}.Tests.csproj"


def manifest(slug: str) -> dict:
    root, _, _ = adapter_paths(slug)
    return json.loads((root / "package.json").read_text(encoding="utf-8"))


def inspect_assembly(path: Path) -> dict:
    return json.loads(tool("inspect-assembly", "--path", str(path), capture=True))


def resolve_game2(candidate: str | None) -> Path | None:
    if not candidate:
        return None
    path = Path(candidate).expanduser().resolve()
    if not path.is_dir():
        return None
    game2 = path if path.name.lower() == "game2" else path / "Game2"
    return game2 if game2.is_dir() else None


def steam_candidates() -> list[Path]:
    if os.name != "nt":
        return []
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
            steam_path = Path(winreg.QueryValueEx(key, "SteamPath")[0])
    except OSError:
        return []
    candidates = [steam_path / "steamapps" / "common" / "SpaceEngineers2"]
    libraries = steam_path / "steamapps" / "libraryfolders.vdf"
    if libraries.is_file():
        for library in re.findall(r'"path"\s+"([^"]+)"', libraries.read_text(encoding="utf-8", errors="replace")):
            candidates.append(Path(library.replace("\\\\", "\\")) / "steamapps" / "common" / "SpaceEngineers2")
    return candidates


def sync_se2(game_directory: str | None) -> Path:
    game2 = resolve_game2(game_directory)
    if game2 is None:
        game2 = next((resolved for candidate in steam_candidates() if (resolved := resolve_game2(str(candidate))) is not None), None)
    if game2 is None:
        raise RuntimeError("Space Engineers 2 was not found. Supply --game-directory <SE2 installation directory>.")
    missing = [name for name in SE2_ASSEMBLIES if not (game2 / name).is_file()]
    if missing:
        raise RuntimeError(f"Required SE2 assemblies are missing from {game2}: {', '.join(missing)}")

    product = inspect_assembly(game2 / "Game2.Client.dll").get("fileVersion") or ""
    version = re.sub(r"[^0-9.]", "", product).strip(".")
    if not version:
        raise RuntimeError(f"Could not normalize the SE2 product version '{product}'.")
    adapter_root, _, _ = adapter_paths("spaceengineers2")
    reference_root = adapter_root / "references"
    destination = reference_root / version
    destination.mkdir(parents=True, exist_ok=True)
    evidence: dict[str, dict] = {}
    for name in SE2_ASSEMBLIES:
        destination_file = destination / name
        shutil.copy2(game2 / name, destination_file)
        evidence[name] = inspect_assembly(destination_file)
    inspection = {
        "schemaVersion": 1,
        "adapterId": "SE2",
        "gameDirectory": str(game2),
        "productVersion": version,
        "inspectedAtUtc": datetime.now(timezone.utc).isoformat(),
        "relevantAssemblies": evidence,
    }
    (destination / "inspection.json").write_text(json.dumps(inspection, indent=2) + "\n", encoding="utf-8")
    (reference_root / "ActiveVersion.props").write_text(
        f"<Project>\n  <PropertyGroup>\n    <SpaceEngineers2ReferenceVersion>{version}</SpaceEngineers2ReferenceVersion>\n  </PropertyGroup>\n</Project>\n",
        encoding="utf-8")
    print(f"Prepared SE2 {version} references and inspection evidence at {destination}")
    return destination


def test_se2(game_directory: str | None, skip_sync: bool) -> None:
    reference = None if skip_sync else sync_se2(game_directory)
    adapter_root, project, tests = adapter_paths("spaceengineers2")
    if reference is None:
        active = adapter_root / "references" / "ActiveVersion.props"
        if not active.is_file():
            raise RuntimeError("SE2 references are not prepared. Run sync-se2 or omit --skip-sync.")
        version = re.search(r"<SpaceEngineers2ReferenceVersion>([^<]+)", active.read_text(encoding="utf-8"))
        if version is None:
            raise RuntimeError("SE2 ActiveVersion.props is invalid.")
        reference = adapter_root / "references" / version.group(1)
    inspection = reference / "inspection.json"
    if not inspection.is_file():
        raise RuntimeError(f"SE2 inspection evidence was not found: {inspection}")
    tool("validate", "adapter", "--adapter", "spaceengineers2")
    tool("validate", "compatibility", "--adapter", "spaceengineers2", "--inspection", str(inspection))
    run("dotnet", "build", str(project), "-c", "Debug")
    run("dotnet", "test", str(tests), "-c", "Debug")
    checklist = reference / "manual-checklist.md"
    if not checklist.exists():
        checklist.write_text(f"""# Space Engineers 2 manual validation checklist

Generated for local game build {reference.name} on {datetime.now(timezone.utc).isoformat()}.

- [ ] Pitch, yaw, and roll: neutral, partial, and maximum input
- [ ] Forward/reverse, strafe, and lift
- [ ] Neutral-stick and deadzone behavior
- [ ] Keyboard and mouse coexistence with Input Control on and off
- [ ] Dampeners, lights, parking brakes, power, and exit grid
- [ ] Primary fire and reload: press, hold, and release
- [ ] Camera mode switch
- [ ] Connection, heartbeat, logs, telemetry, and clean shutdown
- [ ] Every loading method claimed supported by the adapter
""", encoding="utf-8")
        print("SE2 automated compatibility validation completed. Complete the ignored manual checklist before promoting a Tested record.")
    else:
        print("SE2 automated compatibility validation completed. Preserved the existing local manual checklist.")


def test_adapter(slug: str, game_directory: str | None, skip_sync: bool) -> None:
    if slug == "spaceengineers2":
        test_se2(game_directory, skip_sync)
        return
    _, project, tests = adapter_paths(slug)
    tool("validate", "adapter", "--adapter", slug)
    run("dotnet", "build", str(project), "-c", "Debug")
    run("dotnet", "test", str(tests), "-c", "Debug")


def validate_repository() -> None:
    tool("validate", "repository")
    run("dotnet", "test", str(ROOT / "tools" / "Kontrol.AdapterTool.Tests" / "Kontrol.AdapterTool.Tests.csproj"))
    run("dotnet", "test", str(ROOT / "src" / "Adapters" / "DummyAdapter" / "Kontrol.Adapters.DummyAdapter.Tests" / "Kontrol.Adapters.DummyAdapter.Tests.csproj"))
    tracked = run("git", "ls-files", capture=True).splitlines()
    forbidden = [path for path in tracked if re.search(r"^src/Adapters/[^/]+/references/|(^|/)(bin|obj)/|\.(obj|bin|log|dmp|nupkg|snupkg)$", path)]
    if forbidden:
        raise RuntimeError("Tracked forbidden artifacts:\n" + "\n".join(forbidden))


def package(slug: str, version: str, game_directory: str | None, output: str | None, overwrite: bool) -> None:
    data = manifest(slug)
    if data["adapterVersion"] != version:
        raise RuntimeError(f"Requested version {version} does not match manifest version {data['adapterVersion']}.")
    test_adapter(slug, game_directory, False)
    _, project, _ = adapter_paths(slug)
    run("dotnet", "build", str(project), "-c", "Release")
    destination = Path(output).resolve() if output else ROOT / "artifacts" / f"{slug}-{version}.zip"
    arguments = ["pack", "--adapter", slug, "--configuration", "Release", "--output", str(destination)]
    if overwrite:
        arguments.extend(["--overwrite", "true"])
    tool(*arguments)
    tool("verify-package", "--package", str(destination))
    print(f"Created local package: {destination}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("validate")
    sync = commands.add_parser("sync-se2")
    sync.add_argument("--game-directory")
    test = commands.add_parser("test")
    test.add_argument("--adapter", choices=ADAPTERS, required=True)
    test.add_argument("--game-directory")
    test.add_argument("--skip-sync", action="store_true")
    pack = commands.add_parser("pack")
    pack.add_argument("--adapter", choices=ADAPTERS, required=True)
    pack.add_argument("--version", required=True)
    pack.add_argument("--game-directory")
    pack.add_argument("--output")
    pack.add_argument("--overwrite", action="store_true")
    verify = commands.add_parser("verify-package")
    verify.add_argument("--package", required=True)
    descriptor = commands.add_parser("release-descriptor")
    descriptor.add_argument("--adapter", choices=ADAPTERS, required=True)
    descriptor.add_argument("--package", required=True)
    descriptor.add_argument("--package-url", required=True)
    descriptor.add_argument("--tag", required=True)
    descriptor.add_argument("--commit", required=True)
    descriptor.add_argument("--output", required=True)
    descriptor.add_argument("--channel", choices=["stable", "beta"], default="stable")
    catalog = commands.add_parser("catalog-build")
    catalog.add_argument("--releases", required=True)
    catalog.add_argument("--generated-at", required=True)
    catalog.add_argument("--output", required=True)
    catalog_validate = commands.add_parser("catalog-validate")
    catalog_validate.add_argument("--catalog", required=True)
    affected = commands.add_parser("affected")
    affected.add_argument("--base", required=True)
    affected.add_argument("--game-directory")
    args = parser.parse_args()
    try:
        if args.command == "validate": validate_repository()
        elif args.command == "sync-se2": sync_se2(args.game_directory)
        elif args.command == "test": test_adapter(args.adapter, args.game_directory, args.skip_sync)
        elif args.command == "pack": package(args.adapter, args.version, args.game_directory, args.output, args.overwrite)
        elif args.command == "verify-package": tool("verify-package", "--package", args.package)
        elif args.command == "release-descriptor": tool("release", "create", "--adapter", args.adapter, "--package", args.package, "--package-url", args.package_url, "--tag", args.tag, "--commit", args.commit, "--output", args.output, "--channel", args.channel)
        elif args.command == "catalog-build":
            tool("catalog", "build", "--releases", args.releases, "--generated-at", args.generated_at, "--output", args.output)
            tool("catalog", "validate", "--catalog", args.output)
        elif args.command == "catalog-validate": tool("catalog", "validate", "--catalog", args.catalog)
        elif args.command == "affected":
            for slug in tool("affected", "--base", args.base, capture=True).splitlines():
                print(f"Testing affected adapter '{slug}'.")
                test_adapter(slug, args.game_directory, False)
        return 0
    except (RuntimeError, OSError, json.JSONDecodeError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
