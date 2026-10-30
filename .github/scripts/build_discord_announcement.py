#!/usr/bin/env python3
"""Build a player-facing Discord announcement from a published GitHub release."""

from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path


MAX_MESSAGE_LENGTH = 2000
CATALOG_URL = "https://www.komandio.com/kontrol/adapters.html"
TAG_PATTERN = re.compile(
    r"^adapters/(?P<slug>[a-z0-9][a-z0-9-]*)/v(?P<version>[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?)$"
)
COMPATIBILITY_PATTERN = re.compile(
    r"^-\s+Manually tested with (?P<game>.+?) `(?P<version>[^`]+)`"
    r"(?: \(Steam build `(?P<build>[^`]+)`\))?\.?$"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True, help="Published adapter tag")
    parser.add_argument("--repo", required=True, help="GitHub owner/repository")
    parser.add_argument("--output", required=True, type=Path, help="Output text file")
    return parser.parse_args()


def load_release(tag: str, repo: str) -> dict:
    if not TAG_PATTERN.fullmatch(tag):
        raise ValueError("Tag must match adapters/<slug>/v<version>.")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
        raise ValueError("Repository must match owner/name.")
    if not os.environ.get("GH_TOKEN"):
        raise ValueError("GH_TOKEN is required to read the published release.")

    result = subprocess.run(
        ["gh", "release", "view", tag, "--repo", repo,
         "--json", "isDraft,tagName,name,body,assets"],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode:
        raise ValueError(f"Could not read GitHub release: {result.stderr.strip()}")

    release = json.loads(result.stdout)
    if release.get("isDraft"):
        raise ValueError("Refusing to announce a draft release.")
    if release.get("tagName") != tag:
        raise ValueError("GitHub release tag does not match the requested tag.")

    match = TAG_PATTERN.fullmatch(tag)
    assert match is not None
    slug, version = match.group("slug", "version")
    expected_asset = f"kontrol-adapter-{slug}-{version}-win-x64.zip"
    if not any(asset.get("name") == expected_asset for asset in release.get("assets", [])):
        raise ValueError(f"Published release is missing package asset {expected_asset}.")
    return release


def build_message(release: dict) -> str:
    highlights: list[str] = []
    compatibility: str | None = None
    for line in (release.get("body") or "").splitlines():
        line = line.strip()
        compatibility_match = COMPATIBILITY_PATTERN.fullmatch(line)
        if compatibility_match:
            details = compatibility_match.groupdict()
            compatibility = f"🎮 Compatible with {details['game']} {details['version']}"
            if details.get("build"):
                compatibility += f" (Steam build {details['build']})"
        elif line.startswith("- "):
            highlights.append("• " + line[2:].strip())

    if not release.get("name"):
        raise ValueError("Published release has no title.")
    if not highlights:
        raise ValueError("Published release notes contain no player-facing highlights.")

    lines = [f"🚀 **{release['name']}**", "", *highlights]
    if compatibility:
        lines.extend(["", compatibility])
    lines.extend([
        "",
        "📚 Browse the adapter catalog or check for updates in the Kontrol app:",
        CATALOG_URL,
    ])
    message = "\n".join(lines)
    if len(message) > MAX_MESSAGE_LENGTH:
        raise ValueError(
            f"Announcement is {len(message)} characters; Discord allows at most {MAX_MESSAGE_LENGTH}."
        )
    return message


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    args = parse_args()
    try:
        release = load_release(args.tag, args.repo)
        message = build_message(release)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(message + "\n", encoding="utf-8")
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Error building Discord announcement: {exc}", file=sys.stderr)
        return 1

    print(f"Built announcement for {args.tag}: {args.output}")
    print(message)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
