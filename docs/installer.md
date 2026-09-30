# Installer

Two front-ends share the same install logic:

| App | Project | Use |
|-----|---------|-----|
| **Setup GUI** | `src/UneekorRelaConnector.Installer.App` | Windows: browse to `rela.exe`, Install / Uninstall |
| **CLI** | `src/UneekorRelaConnector.Installer` | Scripts / CI: `install --target …` |

Both copy **only** `UneekorRelaConnector.dll` next to `rela.exe` (or `PurePlay.exe`). They never deploy `rela.OtherDevice.Abstractions.dll`.

## GUI (Windows)

```bash
dotnet build src/UneekorRelaConnector.Installer.App/UneekorRelaConnector.Installer.App.csproj -c Release
```

Run `UneekorRelaConnector.Setup.exe`. It ships the plugin in `payload\`. Browse to the host folder if auto-detect misses it, then **Install**.

Afterward: rēlā / PurePlay → Device Type **Other** → Search.

## CLI

```bash
dotnet run --project src/UneekorRelaConnector.Installer -- install --target "C:\Path\To\rela" --dll path\to\UneekorRelaConnector.dll
dotnet run --project src/UneekorRelaConnector.Installer -- uninstall --target "C:\Path\To\rela"
dotnet run --project src/UneekorRelaConnector.Installer -- find --root "C:\Path"
```

`--allow-missing-host` skips the `rela.exe` check (tests / custom host names). `--no-settings` skips writing the default JSON.

## What install does

1. Copy `UneekorRelaConnector.dll` into the host folder (overwrite = upgrade).
2. Create `Settings\Other\` and `Settings\Other\exputt-putts\`.
3. Write default `uneekor-rela-connector.json` if it is missing.

Uninstall removes the DLL and leaves settings.
