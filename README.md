# pureplay-uneekor

Community connector research + scaffold: feed **Uneekor VIEW** shot data into **rēlā / PurePlay** via the open **Other** launch-monitor plugin API.

## Status

PurePlay / rēlā will not natively support Uneekor for now. The in-app open plugin surface is documented here:

- [eKsiSLe/rela-OtherLM](https://github.com/eKsiSLe/rela-OtherLM) — `ILMDevice` plugin contract  
- NuGet: [`rela.OtherDevice.Abstractions`](https://www.nuget.org/packages/rela.OtherDevice.Abstractions)

This repo scaffolds **`UneekorRelaConnector`**: a Windows DLL plugin that watches Uneekor VIEW `ShotData` and emits `OnShotEnded` into the host.

## Docs

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
4. Hit balls.

## Architecture

```
Uneekor VIEW ShotData JSON
        → UneekorRelaConnector.dll (ILMDevice)
        → OnShotEnded(DeviceShotData)
        → rēlā / PurePlay (Device Type = Other)
```

## Prior art

- [springbok/MLM2PRO-GSPro-Connector](https://github.com/springbok/MLM2PRO-GSPro-Connector) — OCR + GSPro patterns  
- [Open-Birdie uneekor-watch](https://github.com/rroojrooj/Open-Birdie) — ShotData field mapping  
- [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html) — reference LM JSON dialect (not required for this plugin path)
