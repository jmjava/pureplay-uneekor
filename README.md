# pureplay-uneekor

Community connector: **Uneekor VIEW** full-swing + **ExPutt** putting into **rēlā / PurePlay** via the open **Other** launch-monitor plugin API.

## Status

PurePlay / rēlā will not natively support Uneekor for now. The in-app open plugin surface is:

- [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) — `ILMDevice` plugin contract  
- NuGet: [`rela.OtherDevice.Abstractions`](https://www.nuget.org/packages/rela.OtherDevice.Abstractions)

**`UneekorRelaConnector`** is a Windows DLL that:

- watches Uneekor VIEW `ShotData` and emits `OnShotEnded` for full swings
- arms **ExPutt** when the host selects putter (`SetPuttingMode` / `SetClub("PT")`)
- accepts putts from a JSON drop folder or a GSPro Open Connect v1 **server** so you can piggyback on [springbok](https://github.com/springbok/MLM2PRO-GSPro-Connector) ExPutt OCR

## Docs

- [Dual-monitor ExPutt putting](docs/exputt-putting.md)
- [Research & plan](docs/research-uneekor-pureplay-connector.md)
- [rēlā Other LM API notes](docs/rela-otherlm-api.md)
- [Connector README](src/UneekorRelaConnector/README.md)

## Quick start (Windows)

```bash
dotnet build src/UneekorRelaConnector/UneekorRelaConnector.csproj -c Release
```

1. Run Uneekor VIEW (Practice) so it writes ShotData.  
2. Copy `UneekorRelaConnector.dll` next to `rela.exe`.  
3. In rēlā / PurePlay: Device Type → **Other** → Search.  
4. Hit balls. For putting, put ExPutt Camera on a **second monitor** and point springbok at `127.0.0.1:921` — see [the putting guide](docs/exputt-putting.md).

## Architecture

```
Uneekor VIEW ShotData  ──►  UneekorRelaConnector.dll (ILMDevice)
ExPutt (springbok OCR) ──►  Open Connect :921  ──┘
                                    │ OnShotEnded
                                    ▼
                          rēlā / PurePlay (Device Type = Other)
```

## Prior art

- [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) — host plugin contract  
- [springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector) — ExPutt OCR + PT club switch (GPL; use their release, do not vendor)  
- [Open-Birdie uneekor-watch](https://github.com/rroojrooj/Open-Birdie) — ShotData field mapping  
- [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html) — putting-client dialect
