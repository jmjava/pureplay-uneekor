# Research: Uneekor → PurePlay Community Connector

**Status:** Research / planning (no public PurePlay open-API schema published yet)  
**Date:** 2026-08-07  
**Goal:** Define a viable path for a community connector that feeds Uneekor launch-monitor shot data into PurePlay.

---

## Executive summary

PurePlay will not natively support Uneekor “at this time.” Their Discord guidance points community builders at:

1. An **open API inside the PurePlay app** (for community plugins), and  
2. Rumors of an **Uneekor plugin** / existing community patterns.

Uneekor does **not** publish a clean public developer SDK for arbitrary sims. Practical integrations today use one of:

| Path | Source of truth | Reliability | Notes |
|------|-----------------|-------------|--------|
| **A. VIEW ShotData folder watch** | `%LOCALAPPDATA%\..\LocalLow\Uneekor\VIEW\ShotData\` | High | Best free/community approach; used by Open-Birdie |
| **B. GSPconnect / Uneekor “OpenAPI” → GSPro Open Connect** | Official Uneekor 3rd-party / GSPro path | Medium | Requires Uneekor Pro / connector entitlements; recent VIEW versions have blacklisted OpenAPI window titles / process names |
| **C. OCR of VIEW Numbers / Multi View** | Screen scrape via Tesseract | Low–Medium | Pattern from [springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector); brittle across VIEW UI/font updates |
| **D. Official Uneekor 3rd-party connector** | Uneekor Launcher | N/A for PurePlay | Only GSPro, E6, TGC 2019, ProTee Play, Creative Golf |

**Recommended architecture:** implement a small Windows bridge:

```
Uneekor VIEW (ShotData JSON)
        │
        ▼
  Uneekor→PurePlay Connector
  (normalize ball/club metrics, heartbeat, reconnect)
        │
        ▼
  PurePlay Open API (community plugin listener)
```

Until PurePlay’s open API schema is captured from the app/Discord, treat the **output adapter as pluggable** and spike the Uneekor ingest side first (it is already well understood).

---

## Discord access (credentials)

**No — do not share Discord username/password with this agent (or any AI/tooling).**

Reasons:

- Personal credentials should never be pasted into chat or agent sessions.
- There is no Discord MCP / bot auth configured in this environment.
- Discord ToS and account security make credential sharing a bad idea.

**Useful alternatives (pick any):**

1. Paste or export the PurePlay Discord thread(s) that mention the open API / Uneekor.  
2. Screenshot or copy the open-API field list / port / sample JSON if someone posted it.  
3. When you have a PurePlay build, capture local open-API docs from the app itself (many sims ship a local HTML/JSON schema or example payload).  
4. If PurePlay later offers a bot/webhook or public docs URL, share that instead of account login.

---

## What we know about PurePlay

Sources: [pureplaygolf.com](https://www.pureplaygolf.com/), public Patreon/LinkedIn updates, Discord quote provided by user.

- **Product:** Unreal Engine sim golf OS; practice + courses + competition. Launching **Fall 2026**; currently waitlist / pre-alpha.
- **Hardware stance:** Explicitly **hardware-agnostic**.
- **Two integration tiers:**
  - **Official integrations** — licensed SDKs in-game; no third-party bridge. Site claims ~8 official, more in progress. Public mentions include Square, Golfjoy, ProTee.
  - **Open community integrations** — independent connectors; **not managed by PurePlay**.
- **Uneekor specifically:** Omitted from native support; community plugins expected.
- **Gap:** No public PurePlay open-API documentation found yet (ports, payload schema, heartbeat, club selection callbacks). This is the main blocker for the PurePlay half of the connector.

### Discord quote (user-provided)

> I cannot support it natively, which is why it is omitted. There is a open source API in the app and I have heard of a Uneekor plugin, but I cannot support them natively at this time. Someday? Perhaps. For now, community plugins will have to do.

Interpretation:

- Native Uneekor SDK integration is out of scope for PurePlay for now.  
- A documented (or at least discoverable) **in-app open API** is the intended community surface.  
- Someone in the community may already be working on / have heard of an Uneekor plugin — worth confirming via Discord once you paste that context.

---

## What we know about Uneekor APIs / feeds

### 1) Official stack (Launcher)

From Uneekor support KB:

- **Uneekor Launcher** — hub for hardware activation, installs, updates, and connecting to Uneekor + selected third-party software.
- **VIEW** — practice range / numbers / video; included with hardware.
- **GameDay / REFINE** — Uneekor first-party sims.
- **Third-party Connector** — official bridge to **GSPro, E6 Connect, TGC 2019, ProTee Play, Creative Golf** only. PurePlay is not listed.

Docs of note:

- [What Uneekor software are available?](https://support.uneekor.com/knowledge/what-uneekor-software-are-available)
- [Third Party Compatibility](https://uneekor.com/golf-simulator-software/third-party)
- Older PDF: [Uneekor 3rd Party Software Interface Guide](https://20033507.fs1.hubspotusercontent-na1.net/hubfs/20033507/Product%20Documents/Launch%20Monitor/Uneekor/Uneekor%203rdParty%20Software%20Interface%20Guide.pdf)

### 2) VIEW ShotData folder (best community ingest)

Documented in practice by [Open-Birdie `tools/uneekor-watch.js`](https://github.com/rroojrooj/Open-Birdie/blob/main/tools/uneekor-watch.js):

- Path: `...\AppData\LocalLow\Uneekor\VIEW\ShotData\`
- Each shot is a numbered folder containing at least:
  - `shotinfo.json` — ball metrics under `DATA`:
    - `ballspeed`, `incline` (VLA), `azimuth` (HLA)
    - `spinmag2d` / `spinaxis2d` / `backspin` / `sidespin`
  - `ProShotInfo.json` — optional club name (`ClubName` / `Club`)
- Works even on free Practice-tier VIEW (per Open-Birdie comments).
- Bridge pattern: poll for new folders → parse JSON → emit normalized shot → send to sim protocol (they use GSPro Open Connect on TCP `127.0.0.1:921`).

**This is the primary Uneekor source we should build against.**

### 3) GSPro Open Connect (reference outbound protocol)

Public docs: [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html)

- TCP `127.0.0.1:921` (no auth)
- LM → sim: JSON with `DeviceID`, `Units`, `ShotNumber`, `APIversion`, `BallData`, optional `ClubData`, `ShotDataOptions`
- Sim → LM: `200` ack, `201` player info (handedness/club), `5xx` errors
- Heartbeat via `ShotDataOptions.IsHeartBeat`

PurePlay’s open API may or may not match this. Many community sims either:

- speak Open Connect directly, or  
- use a similar local TCP JSON dialect (see OpenGolfSim on port `3111`, Nova OpenAPI, Flight Relay WebSocket, etc.).

### 4) OCR path (springbok connector lessons)

[springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector) supports Uneekor via OCR:

- Capture VIEW **Numbers** tab or **Multi View** metrics window.
- Tesseract with `uneekor.traineddata` / `uneekor_ipad.traineddata`.
- Map ROIs for speed, back/side spin, HLA, VLA, club speed, AoA, path.
- Emit to GSPro via their `GSProConnect` client.

Known pain points from issues:

- VIEW UI/font/color changes break OCR (e.g. white-on-black Numbers).
- Uneekor Launcher / VIEW updates have **blacklisted OpenAPI** process names (`GSPconnect`) and window titles (`APIv1 Connect`), causing crashes or blocked connectors — community workarounds involve renaming/hiding processes (fragile, not recommended as primary path).
- OCR is a useful fallback if ShotData disappears or changes format, not the first choice.

### 5) “Uneekor plugin” rumor

Likely refers to one of:

- Community OCR connectors (springbok et al.),  
- ShotData watchers (Open-Birdie style),  
- Or a PurePlay-side community plugin not yet public.

**Action:** when Discord context is available, identify whether that plugin already targets PurePlay’s open API. If yes, collaborate / fork rather than rewrite.

---

## Adjacent open standards (optional future)

Not PurePlay-specific, but useful if PurePlay adopts or mirrors them:

| Spec | Transport | Relevance |
|------|-----------|-----------|
| [GSPro Open Connect](https://gsprogolf.com/GSProConnectV1.html) | TCP JSON :921 | De facto community LM↔sim standard |
| [Flight Relay Protocol](https://github.com/flightrelay/spec) | WebSocket JSON | Emerging open LM event stream |
| OpenGolfSim Developer API | TCP JSON :3111 | Example of sim-side open LM ingest |
| Nova OpenAPI / WS | TCP + WebSocket | Example of LM-side open emit |

**Design implication:** keep an internal `NormalizedShot` model and thin adapters for each protocol.

---

## Proposed connector design

### Components

```
┌─────────────────────┐
│  Uneekor VIEW       │
│  ShotData watcher   │  ← primary ingest
└──────────┬──────────┘
           │ NormalizedShot
┌──────────▼──────────┐
│  Core connector     │  session, units, dedupe, heartbeat, logging
└──────────┬──────────┘
           │
   ┌───────┴────────┐
   ▼                ▼
 PurePlayAdapter  Debug/Replay
 (open API TBD)   (file / GSPro OC for testing)
```

### Internal `NormalizedShot` (draft)

```json
{
  "source": "uneekor-view-shotdata",
  "shotId": "42",
  "timestamp": "2026-08-07T16:00:00Z",
  "units": {
    "speed": "mph",
    "distance": "yards",
    "angles": "degrees",
    "spin": "rpm"
  },
  "ball": {
    "speed": 147.5,
    "vla": 14.3,
    "hla": 2.3,
    "totalSpin": 3250,
    "spinAxis": -13.2,
    "backSpin": 2500,
    "sideSpin": -800
  },
  "club": {
    "name": "7I",
    "speed": null,
    "path": null,
    "faceToTarget": null,
    "angleOfAttack": null
  }
}
```

Map from Uneekor `shotinfo.json` fields:

| Uneekor field | Normalized |
|---------------|------------|
| `DATA.ballspeed` | `ball.speed` (confirm mph vs m/s; Open-Birdie supports `--speed-scale 2.23694`) |
| `DATA.incline` | `ball.vla` |
| `DATA.azimuth` | `ball.hla` (optional invert) |
| `DATA.spinmag2d` / hypot(back,side) | `ball.totalSpin` |
| `DATA.spinaxis2d` | `ball.spinAxis` |
| `DATA.backspin` / `sidespin` | `ball.backSpin` / `sideSpin` |
| `ProShotInfo.ClubName` | `club.name` |

### Runtime UX (MVP)

1. User starts Uneekor Launcher → VIEW (Practice session).  
2. User starts PurePlay with open/community LM mode enabled.  
3. User starts connector tray app.  
4. Connector auto-discovers ShotData path + PurePlay open-API endpoint.  
5. Status: Uneekor feed green / PurePlay linked green / last shot metrics.  
6. Optional: replay last shot, invert HLA, unit conversion.

### Tech stack recommendation

- **Language:** TypeScript (Node) or Python — both have proven community connectors; Node matches Open-Birdie’s watcher; Python matches springbok.
- **Packaging:** Windows tray exe (electron-builder / pyinstaller).
- **Config:** ShotData path override, PurePlay host/port, unit scale, HLA invert.
- **Logging:** rotate local logs for Discord support.

---

## Implementation plan

### Phase 0 — Unblock PurePlay API schema (human + Discord)

- [ ] Collect PurePlay open API docs / sample payloads from Discord or the app.
- [ ] Confirm: transport (TCP / WS), port, auth?, heartbeat, shot fields, player/club callbacks.
- [ ] Ask whether PurePlay already accepts GSPro Open Connect (would collapse scope).
- [ ] Identify any existing “Uneekor plugin” authors in that Discord.

### Phase 1 — Uneekor ingest spike (can start now)

- [ ] Port/adapt Open-Birdie ShotData watcher.
- [ ] Unit tests with fixture `shotinfo.json` samples.
- [ ] Confirm units on target VIEW version(s) (Eye Mini / Lite / XO family as available).
- [ ] Optional: club data from `ProShotInfo.json` when present.

### Phase 2 — PurePlay adapter

- [ ] Implement adapter against confirmed open API.
- [ ] Heartbeat / ready signaling if required.
- [ ] Handle sim→connector player/club messages if present.
- [ ] End-to-end test: swing in VIEW → ball flies in PurePlay.

### Phase 3 — Hardening

- [ ] Reconnect / mid-write JSON retries (Open-Birdie already does this).
- [ ] Deduplicate shot folders; ignore historical shots on start.
- [ ] Putting / low-speed path validation.
- [ ] Fallback OCR path only if ShotData regresses (borrow springbok ROI ideas).
- [ ] Avoid relying on Uneekor OpenAPI blacklist workarounds.

### Phase 4 — Ship

- [ ] Windows installer + auto-start option.
- [ ] README: setup order (VIEW → PurePlay → Connector).
- [ ] Discord support channel checklist (logs, VIEW version, connector version).

---

## Risks & constraints

| Risk | Impact | Mitigation |
|------|--------|------------|
| PurePlay open API undocumented / unstable pre-launch | Cannot finish outbound adapter | Pluggable adapters; spike ingest now |
| Uneekor ShotData format change | Breaks primary ingest | Version detection + fixtures; OCR fallback |
| Uneekor Pro / OpenAPI entitlement enforcement | GSPconnect path blocked | Prefer ShotData; do not depend on paid GSPconnect |
| Process blacklists / anti-community measures | OCR/OpenAPI bridges break | Stay on file-watch; no process hacks as core design |
| Legal / ToS | Ambiguous for reverse-engineered paths | Prefer publicly written files + PurePlay’s invited community API |

---

## Reference links

### PurePlay
- https://www.pureplaygolf.com/

### Uneekor
- https://support.uneekor.com/knowledge/what-uneekor-software-are-available
- https://uneekor.com/golf-simulator-software/third-party
- https://uneekor.com/golf-simulator-software/view

### Community connectors & protocols
- https://github.com/springbok/MLM2PRO-GSPro-Connector (OCR + GSPro; Uneekor wiki page)
- https://github.com/springbok/MLM2PRO-GSPro-Connector/wiki/Uneekor
- https://github.com/rroojrooj/Open-Birdie (ShotData → Open Connect)
- https://gsprogolf.com/GSProConnectV1.html
- https://github.com/flightrelay/spec
- https://help.opengolfsim.com/desktop/apis/shot-data/ (example sim-side open LM API)

---

## Immediate next asks for you

1. **Paste PurePlay Discord open-API details** (port, sample JSON, docs link) — do **not** send Discord password.  
2. Confirm which Uneekor hardware + VIEW version you have (Eye Mini Lite / Eye Mini / Eye XO / etc.).  
3. Confirm whether you already have Uneekor **Pro Package** / GSPconnect (nice-to-have only; not required if ShotData works).  
4. If you find the rumored Uneekor↔PurePlay plugin, share the repo/Discord post so we can align rather than duplicate.
