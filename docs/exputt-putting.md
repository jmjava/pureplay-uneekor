# Dual-monitor Uneekor + ExPutt putting

Goal: full-swing shots from **Uneekor VIEW**, putts from **ExPutt**, both into rēlā / PurePlay on one plugin.

```
Monitor 1 (projector / sim)          Monitor 2 (putting cam)
┌──────────────────────────┐         ┌──────────────────────────┐
│ rēlā / PurePlay          │         │ Windows Camera + ExPutt  │
│ Device Type = Other      │         │ (maximized, ROIs stable) │
└────────────▲─────────────┘         └────────────▲─────────────┘
             │                                    │
             │ OnShotEnded                        │ OCR when club = PT
             │                                    │
     UneekorRelaConnector.dll        springbok MLM2PRO-GSPro-Connector
             │                                    │
             ├─ VIEW ShotData (NORMAL)            │
             └─ Open Connect :921 (PUTTING) ◄─────┘
```

This is the same dual-source pattern as [springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector): full-swing LM until the sim selects **PT**, then ExPutt.

We do **not** embed Tesseract or window-capture in the rēlā DLL. ExPutt has no public file API. Springbok already owns that OCR (Camera window + `exputt.traineddata`). This plugin implements the **host** half: a GSPro Open Connect v1 **server** plus an optional JSON file drop.

## Hardware / display layout

1. **Monitor 1** — rēlā / PurePlay (and Uneekor VIEW if you want it).  
2. **Monitor 2** — Windows **Camera** app with the ExPutt overlay, **maximized** before you set ROIs.  
   Springbok sizes the Camera window from saved `window_rect`. A non-maximized or moved window is the usual “no readings” failure ([issue 177](https://github.com/springbok/MLM2PRO-GSPro-Connector/issues/177)).
3. Keep ExPutt synced and the ball recognized on the mat before you putt.

Setup videos (springbok / community):

- [ExPutt + connector](https://www.youtube.com/watch?v=dV0CH2Vy0Y0)
- [MLM2PRO + ExPutt + GSPro](https://www.youtube.com/watch?v=9wt06I_euHs)

## Plugin settings

Device Settings (or `Settings/Other/uneekor-rela-connector.json` next to `rela.exe`):

| Field | Default | Meaning |
|-------|---------|---------|
| `PuttingSource` | `Both` | `None` / `File` / `OpenConnect` / `Both` |
| `OpenConnectBind` | `127.0.0.1` | Listen address for springbok |
| `OpenConnectPort` | `921` | GSPro Open Connect port |
| `PuttingDirectory` | `Settings/Other/exputt-putts` | JSON drop folder |
| `IgnoreUneekorWhilePutting` | `true` | Drop VIEW ShotData while PT / PUTTING |
| `AutoPuttingOnPutterClub` | `true` | `SetClub("PT")` arms ExPutt |
| `InvertPuttHla` / `PuttSpeedScale` | off / `1` | Putt-only corrections |

`Discover()` / Search starts the live session (Uneekor watcher + putting listeners).

## Path A — piggyback on springbok (recommended)

1. Build this plugin and copy **only** `UneekorRelaConnector.dll` next to `rela.exe`.  
2. rēlā / PurePlay → Device Type **Other** → Search. Status should mention `ExPutt Open Connect 127.0.0.1:921`.  
3. Install [springbok](https://github.com/springbok/MLM2PRO-GSPro-Connector/releases). Putting system = **ExPutt**.  
4. In springbok, set GSPro / Open Connect host to `127.0.0.1` and port `921` (this plugin, not GSPro).  
5. Maximize Camera on monitor 2, set ExPutt ROIs, Verify, Save.  
6. Start springbok putting. When the sim selects putter, this plugin sends Open Connect `201` with `Club: PT`; springbok starts OCR and sends the putt JSON here.  
7. The plugin emits `OnShotEnded` with `OTHER_PUTT_LIKE=1`.

Do **not** also run GSPro on port 921. If you still use GSPro, change `OpenConnectPort` here and point springbok at that port.

## Path B — JSON file drop (no OCR)

Drop a file into `Settings/Other/exputt-putts` (processed files move to `processed/`):

```json
{
  "source": "exputt",
  "speed": 4.2,
  "hla": -1.5,
  "vla": 0,
  "path": 0.3,
  "faceToTarget": 0.1
}
```

GSPro Open Connect shot JSON is also accepted. From this repo:

```bash
python3 tools/send-putt.py --file
python3 tools/send-putt.py --tcp 127.0.0.1 921
```

Putts are ignored unless the host is in putting mode (`SetPuttingMode` or club `PT`).

## Mode switching

| Host action | Armed source |
|-------------|--------------|
| `SetNormalMode` / non-PT `SetClub` | Uneekor VIEW ShotData |
| `SetPuttingMode` / `SetClub("PT")` | ExPutt (file and/or Open Connect) |

That matches springbok: OCR runs only while the selected club is `PT`.

## What we did not copy

Springbok is GPL. This repo does **not** vendor their screenshot/OCR/ROI UI. We implement the public [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html) server and the [rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) `ILMDevice` contract. Use their release for Camera OCR; use this DLL for rēlā / PurePlay.
