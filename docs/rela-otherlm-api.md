# rēlā / PurePlay “Other LM” open plugin API

Source: [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) (MIT)  
Contract package: [`rela.OtherDevice.Abstractions` 1.0.0 on NuGet](https://www.nuget.org/packages/rela.OtherDevice.Abstractions)  
Last reviewed: 2026-08-07

## Naming note

The published API and host binary are branded **rēlā** (`rela.exe`, namespace `relaDevicePlugin`). This matches the PurePlay Discord guidance of an “open source API in the app” for community plugins. Confirm on a PurePlay / rēlā build whether the product name in-UI is PurePlay, rēlā, or both — the plugin contract below is what we implement against.

## How plugins load

1. User sets Device Type to **Other**.
2. `PluginFinder` scans `.dll` files next to `rela.exe`, loads assemblies, and registers public non-abstract types implementing `ILMDevice`.
3. For Other, it picks the first registered `ILMDevice` that is not a built-in family.
4. Lifecycle:
   - `Init()` on select
   - `Discover()` on Search (Search does **not** auto-call `Connect()` afterward)
   - `Connect()` on explicit connect
5. Host consumes shot/state events and routes into telemetry + simulator transport.

**Deploy rule:** copy only your connector DLL next to `rela.exe`. Do **not** ship `rela.OtherDevice.Abstractions` / `relaDevicePlugin.dll` — the host embeds the runtime contract.

## Required contract (`ILMDevice`)

Namespace: `relaDevicePlugin`  
Target: `net6.0-windows` (example uses WPF for optional settings UI)

### Events

| Event | Role |
|-------|------|
| `OnBallData(DeviceShotData)` | In-flight / pre-final telemetry (0..N) |
| `OnShot(DeviceShotData)` | Legacy separator; **not** wired to transport in current rēlā |
| `OnShotEnded(DeviceShotData)` | **Primary final shot** — emit once when metrics are final |
| `OnRawShot(DeviceRawShot)` | Optional raw logging when enabled |
| `OnNotification(string)` | Lifecycle / status (see prefixes below) |
| `OnHandedChange(string)` | `"RH"` / `"LH"` |
| `OnModeChange(string)` | e.g. `NORMAL` / `PUTTING` / `CHIPPING` |
| `OnError(string)` | Non-fatal errors |
| `OnNote(string)` | Notes / ball status strings |

### Methods

`Init`, `GetDeviceName`, `Discover`, `Connect`, `Reconnect`, `Disconnect`,  
`SetRightHanded`, `SetLeftHanded`, `SetPuttingMode`, `SetChippingMode`, `SetNormalMode`, `ResetReady`

### Optional reflection extension points

Properties (defaults if absent):

- `OtherIsReady`, `OtherBallPresent`, `OtherIsArmed`
- `SessionConnected`
- `UsesTelemetryFinalShotPath` — set `true` if every mode emits one authoritative final via `OnShotEnded` (recommended for Uneekor)
- `LastStatusUtcTicks` — 25s activity watchdog; refresh on transport activity
- `SupportedConnectionTypes` — e.g. `"Direct"`, `"Network"`, `"USB,Network"`

Optional methods:

- `ArmOnly()` — Arm UI / force-arm
- `SetClub(string club)` — sim club sync

Optional interface:

- `IDeviceSettingsProvider.ShowDeviceSettings()` — Device Settings button in host Settings window

### Lifecycle notification prefixes (exact)

- Connected: `[Other] Connected: ...`
- Disconnected / failed search: `[Other] Disconnected: ...`
- Ball/ready refresh: `[Other] BallStatus: ready=true ball=false`

Emit Connected only after transport handshake is complete.

### Putting note

If the device itself identifies a putt, include note `OTHER_PUTT_LIKE=1` so true-putt handling works even before club selection becomes `PT`.

## Shot payload (`DeviceShotData`)

Core fields used by the example / recommended for Uneekor mapping:

| Field | Notes |
|-------|--------|
| `Speed` | Ball speed |
| `HLA`, `VLA` | Launch direction / angle |
| `BackSpin`, `SideSpin`, `SpinAxis`, `TotalSpin` | Spin |
| `CarryDistance` | Optional |
| `IsShotValid` | Required accuracy |
| `Notes` | Diagnostics / `OTHER_PUTT_LIKE=1` |
| `ClubSpeed`, `AttackAngle` | Club metrics when available |
| `ClubPathHorizontalDeg`, `ClubPathVerticalDeg` | Club path |
| `FaceAngleDeg` | Face angle |
| `ImpactHorizontalMm`, `ImpactVerticalMm` | Impact location |
| `*Confidence` fields | Optional quality |

Also present on the ref assembly: `DeviceRawShot` (`TotalSpeedMPH`, `TotalSpin`, `Carry`, `InsertedAt`, …), `IDeviceTargetAlignment`, `ShotPhase`.

**Preferred plugin pattern:** `UsesTelemetryFinalShotPath = true` and emit each completed measurement once via `OnShotEnded` (do not also duplicate through `OnBallData`).

## Recommended Uneekor → rēlā mapping

| Uneekor `shotinfo.json` / ProShotInfo | `DeviceShotData` |
|--------------------------------------|------------------|
| `DATA.ballspeed` | `Speed` (confirm mph; scale if m/s) |
| `DATA.azimuth` | `HLA` (optional invert) |
| `DATA.incline` | `VLA` |
| `DATA.backspin` | `BackSpin` |
| `DATA.sidespin` | `SideSpin` |
| `DATA.spinaxis2d` | `SpinAxis` |
| `DATA.spinmag2d` or hypot(back,side) | `TotalSpin` |
| `ProShotInfo.ClubName` | feed `SetClub` / notes; club numeric fields if present |
| low-speed / putt heuristic | `Notes += OTHER_PUTT_LIKE=1` |

Ingest path: watch `%LOCALAPPDATA%\..\LocalLow\Uneekor\VIEW\ShotData\` for new folders (Open-Birdie pattern).

## Deploy / test checklist

1. `dotnet build` Uneekor connector (`net6.0-windows`).
2. Copy connector DLL next to `rela.exe`.
3. Open rēlā / PurePlay → Device Type **Other** → Search.
4. Ensure `Discover()` starts or completes connect (host will not call `Connect()` after Search).
5. Confirm `[Other] Connected:` + BallStatus notes in logs.
6. Hit a ball in VIEW → expect `OnShotEnded` → ball flies in sim.

## Implications for our plan

This **unblocks the PurePlay/rēlā half** of the connector. We no longer need a TCP Open Connect guess for the outbound side — we implement `ILMDevice` as an in-process DLL plugin.

Architecture becomes:

```
Uneekor VIEW ShotData (file watch)
        │
        ▼
UneekorRelaConnector.dll  (ILMDevice plugin)
        │ OnShotEnded(DeviceShotData)
        ▼
rēlā / PurePlay host (Device Type = Other)
```
