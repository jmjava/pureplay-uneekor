# UneekorRelaConnector

Community **Other** launch-monitor plugin for [rēlā](https://github.com/eKsiSLe/rela-OtherLM) / PurePlay that reads Uneekor VIEW `ShotData` and emits final shots via `OnShotEnded`.

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

## Settings

Device Settings (when host shows the button) or edit:

`Settings/Other/uneekor-rela-connector.json` next to the host/plugin:

```json
{
  "Handedness": "RH",
  "Mode": "NORMAL",
  "ShotDataDirectory": null,
  "SpeedScale": 1,
  "InvertHla": false
}
```

- Default ShotData path: `%LOCALAPPDATA%\..\LocalLow\Uneekor\VIEW\ShotData`
- If distances are ~2.2× short, set `SpeedScale` to `2.23694` (m/s → mph).

## Architecture

```
Uneekor VIEW → ShotData/<n>/shotinfo.json
        → UneekorRelaConnector (ILMDevice)
        → OnShotEnded(DeviceShotData)
        → rēlā / PurePlay
```

Based on:

- [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) plugin contract
- [Open-Birdie uneekor-watch.js](https://github.com/rroojrooj/Open-Birdie/blob/main/tools/uneekor-watch.js) ShotData fields
