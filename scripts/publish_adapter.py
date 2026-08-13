#!/usr/bin/env python3
"""Interactive local release wizard for a Kontrol adapter.

The local machine validates, tests, and packs. The GitHub release workflow then
signs the descriptor/catalog with KONTROL_SIGNING_PRIVATE_KEY and publishes the
ZIP plus metadata to the repository's public GitHub Pages site.
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent
HELPER = ROOT / "scripts" / "kontrol_adapters.py"
ADAPTERS = ("dummyadapter", "spaceengineers2")
VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")


def run(*command: str, capture: bool = False) -> str:
    print(f"\n> {' '.join(command)}")
    result = subprocess.run(command, cwd=ROOT, text=True, capture_output=capture)
    if capture:
        print(result.stdout, end="")
        print(result.stderr, end="", file=sys.stderr)
    if result.returncode:
        raise RuntimeError(f"Command failed with exit code {result.returncode}.")
    return result.stdout.strip() if capture else ""


def confirm(prompt: str) -> None:
    if input(f"\n{prompt}\nType YES to continue: ").strip() != "YES":
        raise KeyboardInterrupt("Stopped by user. No further action was taken.")


def heading(text: str) -> None:
    print(f"\n{'=' * 72}\n{text}\n{'=' * 72}")


def main() -> int:
    heading("Kontrol Adapter Release Wizard")
    print("This wizard does local work only until the final confirmation:\n"
          "  • your PC validates, tests, and creates the ZIP;\n"
          "  • the final step creates a private GitHub Release containing that ZIP;\n"
          "  • GitHub Actions signs the ZIP and its metadata with the repository secret and deploys only\n"
          "    the ZIP, signed descriptor, and signed catalog to public GitHub Pages.\n"
          "\nBefore starting, update and commit the adapter version, changelog, and\ncompatibility evidence. Never publish a version or tag that already exists.")
    confirm("Have the source/version changes been committed and reviewed?")

    heading("Step 1 of 6 — Choose the adapter")
    for index, adapter in enumerate(ADAPTERS, start=1):
        print(f"  {index}. {adapter}")
    choice = input("Choose an adapter number: ").strip()
    if not choice.isdigit() or not 1 <= int(choice) <= len(ADAPTERS):
        raise ValueError("Choose one of the displayed adapter numbers.")
    adapter = ADAPTERS[int(choice) - 1]
    folder = "SpaceEngineers2" if adapter == "spaceengineers2" else "DummyAdapter"
    manifest = json.loads((ROOT / "src" / "Adapters" / folder / "package.json").read_text(encoding="utf-8"))
    version = str(manifest["adapterVersion"])
    if not VERSION_PATTERN.fullmatch(version):
        raise RuntimeError(f"Invalid adapterVersion in package.json: {version}")
    channel = "beta" if "-" in version else "stable"
    tag = f"adapters/{adapter}/v{version}"
    package = ROOT / "artifacts" / f"kontrol-adapter-{adapter}-{version}-win-x64.zip"

    heading("Step 2 of 6 — Release summary and safety checks")
    print(f"Adapter: {adapter}\nVersion: {version}\nChannel: {channel}\nTag: {tag}\nPackage: {package}")
    if channel == "beta":
        print("Kontrol will hide this beta by default; Pro users must enable Include beta.")
    else:
        print("This is a stable release and becomes the default catalog channel.")
    if run("git", "status", "--porcelain", capture=True):
        raise RuntimeError("kontrol-adapters has uncommitted changes. Commit or stash them first.")
    if run("git", "tag", "--list", tag, capture=True) or run("git", "ls-remote", "--tags", "origin", f"refs/tags/{tag}", capture=True):
        raise RuntimeError(f"The immutable tag already exists: {tag}")
    if package.exists():
        raise RuntimeError(f"Refusing to overwrite an existing local package: {package}")
    secrets = run("gh", "secret", "list", "--repo", "komandio-labs/kontrol-adapters", capture=True)
    if "KONTROL_SIGNING_PRIVATE_KEY" not in secrets:
        raise RuntimeError("GitHub secret KONTROL_SIGNING_PRIVATE_KEY is missing; GitHub cannot sign the publication.")
    confirm("Run validation, targeted tests, and deterministic package creation now?")

    heading("Step 3 of 6 — Local validation, testing, and packaging")
    run(sys.executable, str(HELPER), "validate")
    test = [sys.executable, str(HELPER), "test", "--adapter", adapter]
    if adapter == "spaceengineers2":
        game_directory = input("Optional SE2 install directory (blank uses Steam discovery): ").strip().strip('"')
        if game_directory:
            test.extend(["--game-directory", game_directory])
    run(*test)
    run(sys.executable, str(HELPER), "pack", "--adapter", adapter, "--version", version, "--output", str(package))
    print(f"\nThe verified local ZIP is ready:\n  {package}")
    confirm("Have you reviewed the local test result and package, and want to create a private GitHub Release?")

    heading("Step 4 of 6 — Create the immutable source tag and private release")
    print("This pushes the source tag and creates a PRIVATE repository release with the ZIP.\n"
          "No public Pages file has been written yet.")
    confirm(f"Create tag {tag} and upload {package.name} to GitHub now?")
    run("git", "tag", "-a", tag, "-m", f"Release {adapter} v{version} ({channel})")
    run("git", "push", "origin", tag)
    release_args = ["gh", "release", "create", tag, str(package), "--repo", "komandio-labs/kontrol-adapters",
                    "--title", f"{adapter} {version}", "--notes", f"{channel.capitalize()} adapter release {version}."]
    if channel == "beta":
        release_args.append("--prerelease")
    run(*release_args)

    heading("Step 5 of 6 — GitHub signs and deploys")
    print("GitHub Actions now downloads the local ZIP from the private release, adds and verifies its publisher signature,\n"
          "creates a signed descriptor, rebuilds and signs the catalog, and publishes generated files to the gh-pages branch.\n"
          "Open the Actions run and wait for it to pass before testing Kontrol.")
    run("gh", "run", "list", "--repo", "komandio-labs/kontrol-adapters", "--workflow", "publish-adapter-pages.yml", "--limit", "3")
    print("\nAfter the workflow succeeds and Pages deploys, verify these anonymous URLs:\n"
          "  https://komandio-labs.github.io/kontrol-adapters/catalog/v1/catalog.json\n"
          f"  https://komandio-labs.github.io/kontrol-adapters/packages/{adapter}/{version}/{package.name}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt as error:
        print(f"\nStopped: {error}", file=sys.stderr)
        raise SystemExit(1)
    except Exception as error:
        print(f"\nRelease wizard stopped: {error}", file=sys.stderr)
        raise SystemExit(1)
