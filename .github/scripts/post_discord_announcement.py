#!/usr/bin/env python3
"""List Discord channels or post an announcement through a bot."""

from __future__ import annotations

import argparse
import getpass
import json
import os
import sys
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen


API_BASE = "https://discord.com/api/v10"
MAX_MESSAGE_LENGTH = 2000
DEFAULT_CHANNEL_ID = "1541835027980230706"
DEFAULT_GUILD_ID = "1427868551116488877"
CHANNEL_TYPES = {
    0: "text",
    2: "voice",
    4: "category",
    5: "announcement",
    13: "stage",
    15: "forum",
    16: "media",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="List server channels or post a message using a Discord bot."
    )
    parser.add_argument(
        "--list-channels",
        action="store_true",
        help="List channel names, categories, types, and IDs (read-only).",
    )
    parser.add_argument(
        "--guild-id",
        default=DEFAULT_GUILD_ID,
        help=f"Server ID for --list-channels (default: {DEFAULT_GUILD_ID}).",
    )
    parser.add_argument(
        "--channel-id",
        help=f"Target text channel ID for posting (default: {DEFAULT_CHANNEL_ID}).",
    )
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--message", help="Message text to post.")
    source.add_argument(
        "--file",
        type=Path,
        help="UTF-8 text file containing the message to post.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.list_channels:
        if not args.guild_id or not args.guild_id.isdecimal():
            print("Error: --list-channels requires a numeric --guild-id.", file=sys.stderr)
            return 2
        if args.channel_id or args.message is not None or args.file is not None:
            print("Error: channel listing cannot be combined with posting options.", file=sys.stderr)
            return 2
        return list_channels(args.guild_id)

    channel_id = args.channel_id or DEFAULT_CHANNEL_ID
    if not channel_id.isdecimal():
        print("Error: channel ID must contain only digits.", file=sys.stderr)
        return 2
    if (args.message is None) == (args.file is None):
        print("Error: provide exactly one of --message or --file.", file=sys.stderr)
        return 2

    try:
        content = (
            args.message
            if args.message is not None
            else args.file.read_text(encoding="utf-8")
        )
    except OSError as exc:
        print(f"Error reading message file: {exc}", file=sys.stderr)
        return 2

    content = content.strip()
    if not content:
        print("Error: message is empty.", file=sys.stderr)
        return 2
    if len(content) > MAX_MESSAGE_LENGTH:
        print(f"Error: Discord messages can be at most {MAX_MESSAGE_LENGTH} characters.", file=sys.stderr)
        return 2

    print(f"Target channel: {channel_id}")
    print("Sending message:\n")
    print(content)

    token = read_bot_token()
    if token is None:
        print("Error: token was not provided; nothing was sent.", file=sys.stderr)
        return 2

    payload = json.dumps(
        {"content": content, "allowed_mentions": {"parse": []}}
    ).encode("utf-8")
    request = Request(
        f"{API_BASE}/channels/{channel_id}/messages",
        data=payload,
        headers={
            "Authorization": f"Bot {token}",
            "Content-Type": "application/json",
            "User-Agent": "KontrolAnnouncementPoster/1.0",
        },
        method="POST",
    )

    try:
        with urlopen(request, timeout=20) as response:
            result = json.loads(response.read().decode("utf-8"))
        print(f"Posted successfully. Discord message ID: {result.get('id', 'unknown')}")
        return 0
    except HTTPError as exc:
        # Do not print request headers or token; report Discord's response only.
        try:
            body = json.loads(exc.read().decode("utf-8"))
            detail = body.get("message", "Discord returned an error.")
        except (UnicodeDecodeError, json.JSONDecodeError):
            detail = "Discord returned an unreadable error response."
        print(f"Discord API error ({exc.code}): {detail}", file=sys.stderr)
        return 1
    except (TimeoutError, URLError) as exc:
        print(f"Could not reach Discord: {exc}", file=sys.stderr)
        return 1


def list_channels(guild_id: str) -> int:
    token = read_bot_token()
    if token is None:
        print("Error: token was not provided; nothing was requested.", file=sys.stderr)
        return 2

    request = Request(
        f"{API_BASE}/guilds/{guild_id}/channels",
        headers={
            "Authorization": f"Bot {token}",
            "User-Agent": "KontrolAnnouncementPoster/1.0",
        },
        method="GET",
    )
    try:
        with urlopen(request, timeout=20) as response:
            channels = json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        return report_api_error(exc)
    except (TimeoutError, URLError) as exc:
        print(f"Could not reach Discord: {exc}", file=sys.stderr)
        return 1
    except (UnicodeDecodeError, json.JSONDecodeError):
        print("Discord returned an unreadable channel list.", file=sys.stderr)
        return 1

    categories = {c["id"]: c for c in channels if c.get("type") == 4}
    visible = [c for c in channels if c.get("type") != 4]
    visible.sort(key=lambda c: (c.get("position", 0), c.get("name", "").casefold()))
    uncategorized = [c for c in visible if c.get("parent_id") not in categories]
    print(f"Channels in server {guild_id} (threads are not included):")
    for channel in uncategorized:
        print_channel(channel)
    for category in sorted(categories.values(), key=lambda c: c.get("position", 0)):
        children = [c for c in visible if c.get("parent_id") == category["id"]]
        print(f"\n{category.get('name', 'Unnamed category')}/")
        for channel in children:
            print_channel(channel, indent="  ")
    if not visible:
        print("No channels were returned. Check the server ID and bot access.")
    return 0


def read_bot_token() -> str | None:
    token = os.environ.get("DISCORD_BOT_TOKEN", "").strip()
    if token:
        return token
    if not sys.stdin.isatty():
        return None
    token = getpass.getpass("Discord bot token (input hidden): ").strip()
    return token or None


def print_channel(channel: dict, indent: str = "") -> None:
    kind = CHANNEL_TYPES.get(channel.get("type"), f"type {channel.get('type', '?')}")
    print(f"{indent}#{channel.get('name', 'unnamed')} [{kind}] — {channel.get('id', '?')}")


def report_api_error(exc: HTTPError) -> int:
    # Do not print request headers or token; report Discord's response only.
    try:
        body = json.loads(exc.read().decode("utf-8"))
        detail = body.get("message", "Discord returned an error.")
    except (UnicodeDecodeError, json.JSONDecodeError):
        detail = "Discord returned an unreadable error response."
    print(f"Discord API error ({exc.code}): {detail}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
