# pureplay-uneekor

Research and planning for a **community connector** that feeds Uneekor launch-monitor shot data into [PurePlay](https://www.pureplaygolf.com/).

PurePlay does not natively support Uneekor today; their guidance is to use the in-app open API via community plugins.

## Docs

- **[Research & plan](docs/research-uneekor-pureplay-connector.md)** — Uneekor feeds, PurePlay open-API gaps, recommended architecture, phased implementation plan.

## Current status

- PurePlay open API schema is **not publicly documented** yet (pre-launch / waitlist).
- Uneekor ingest is well understood via VIEW `ShotData` JSON (preferred) and OCR (fallback).
- Next unblocker: capture PurePlay open-API sample payloads from Discord or the app build — **without sharing Discord login credentials**.

## Related prior art

- [springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector) — OCR + GSPro Open Connect patterns (includes Uneekor).
- [rroojrooj/Open-Birdie](https://github.com/rroojrooj/Open-Birdie) — Uneekor VIEW `ShotData` folder watcher → Open Connect.
- [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html) — common LM↔sim TCP JSON protocol.
