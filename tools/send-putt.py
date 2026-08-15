#!/usr/bin/env python3
"""Inject a test ExPutt into UneekorRelaConnector (file drop or Open Connect)."""

from __future__ import annotations

import argparse
import json
import socket
import time
from pathlib import Path

DEFAULT_PUTT = {
    "DeviceID": "send-putt",
    "Units": "Yards",
    "ShotNumber": 1,
    "APIversion": "1",
    "BallData": {
        "Speed": 4.2,
        "SpinAxis": 0.0,
        "TotalSpin": 0.0,
        "HLA": -1.5,
        "VLA": 0.0,
        "CarryDistance": 0.0,
    },
    "ClubData": {
        "Speed": 4.2,
        "FaceToTarget": 0.1,
        "Path": 0.3,
    },
    "ShotDataOptions": {
        "ContainsBallData": True,
        "ContainsClubData": True,
        "IsHeartBeat": False,
    },
}


def send_file(directory: Path, payload: dict) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"putt-{int(time.time() * 1000)}.json"
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"Wrote {path}")


def send_tcp(host: str, port: int, payload: dict) -> None:
    data = json.dumps(payload).encode("utf-8")
    with socket.create_connection((host, port), timeout=5) as sock:
        sock.sendall(data)
        sock.settimeout(3)
        try:
            reply = sock.recv(4096)
        except TimeoutError:
            reply = b""
    print(f"Sent {len(data)} bytes to {host}:{port}")
    if reply:
        print(reply.decode("utf-8", errors="replace"))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--file", action="store_true", help="drop JSON into the putt folder")
    parser.add_argument("--tcp", nargs=2, metavar=("HOST", "PORT"), help="send Open Connect JSON")
    parser.add_argument(
        "--dir",
        default="Settings/Other/exputt-putts",
        help="putt JSON folder (with --file)",
    )
    parser.add_argument("--speed", type=float, default=4.2)
    parser.add_argument("--hla", type=float, default=-1.5)
    args = parser.parse_args()

    payload = json.loads(json.dumps(DEFAULT_PUTT))
    payload["BallData"]["Speed"] = args.speed
    payload["BallData"]["HLA"] = args.hla
    payload["ClubData"]["Speed"] = args.speed

    if args.file:
        send_file(Path(args.dir), payload)
    if args.tcp:
        send_tcp(args.tcp[0], int(args.tcp[1]), payload)
    if not args.file and not args.tcp:
        parser.error("specify --file and/or --tcp HOST PORT")


if __name__ == "__main__":
    main()
