# UneekorRelaConnector

Community **Other** launch-monitor plugin for [rēlā](https://github.com/eKsiSLe/rela-OtherLM) / PurePlay.

- Full swing: Uneekor VIEW `ShotData` → `OnShotEnded`
- Putting: ExPutt via JSON drop or GSPro Open Connect (springbok OCR client)

Dual-monitor setup: [docs/exputt-putting.md](../../docs/exputt-putting.md)

## Build (Windows)

```bash
dotnet build src/UneekorRelaConnector/UneekorRelaConnector.csproj -c Release
```

Output: `src/UneekorRelaConnector/bin/Release/net6.0-windows/UneekorRelaConnector.dll`

## Deploy

1. Start Uneekor Launcher → VIEW (Practice) so ShotData is written.
2. Copy `UneekorRelaConnector.dll` next to `rela.exe` (do **not** copy the Abstractions DLL).
3. Open rēlā / PurePlay → Device Type **Other** → Search.
4. Hit balls. Status should show Connected and shots should dispatch.
5. For putting: maximize ExPutt Camera on monitor 2, run springbok with GSPro host `127.0.0.1:921`, select putter in the sim.

## Settings

Device Settings (when host shows the button) or edit:

`Settings/Other/uneekor-rela-connector.json` next to the host/plugin:

```json
{
  "Handedness": "RH",
  "Mode": "NORMAL",
  "ShotDataDirectory": null,
  "SpeedScale": 1,
  "InvertHla": false,
  "PuttingSource": "Both",
  "PuttingDirectory": null,
  "OpenConnectBind": "127.0.0.1",
  "OpenConnectPort": 921,
  "IgnoreUneekorWhilePutting": true,
  "AutoPuttingOnPutterClub": true,
  "InvertPuttHla": false,
  "PuttSpeedScale": 1
}
```

- Default ShotData path: `%LOCALAPPDATA%\..\LocalLow\Uneekor\VIEW\ShotData`
- If distances are ~2.2× short, set `SpeedScale` to `2.23694` (m/s → mph).
- `PuttingSource`: `None` | `File` | `OpenConnect` | `Both`
- Default putt drop folder: `Settings/Other/exputt-putts`

## Architecture

```
Uneekor VIEW → ShotData/<n>/shotinfo.json  ─┐
ExPutt → springbok OCR → TCP :921          ─┼─► UneekorRelaConnector
ExPutt → JSON file drop                    ─┘         │ OnShotEnded
                                                      ▼
                                              rēlā / PurePlay
```

Based on:

- [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) plugin contract
- [Open-Birdie uneekor-watch.js](https://github.com/rroojrooj/Open-Birdie/blob/main/tools/uneekor-watch.js) ShotData fields
- [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html) for the putting client
- [springbok ExPutt putting](https://github.com/springbok/MLM2PRO-GSPro-Connector) as the OCR sidecar (not vendored)
