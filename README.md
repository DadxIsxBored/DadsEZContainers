# DadsEZContainers

DadsEZContainers is an independently implemented nearby-container resource system for Valheim 1.0.12. It uses current Valheim container, crafting, building, and processing-station APIs and has its own identifiers, configuration, rules parser, and assets.

The dropped-item pickup delay is configurable from `0.1` through `10.0` seconds in one-decimal increments. Its default is the vanilla `0.5` seconds.

## Core behavior

- Craft and upgrade using items in accessible containers within range.
- Build using nearby container materials.
- Display combined inventory/container totals in requirement panels.
- Feed fires, cooking stations, kilns, smelters, and furnaces from containers.
- Restrict pulling by container prefab, station prefab, item prefab, or group.
- Retain one item in each source container when configured.
- Toggle container access with `LeftAlt + O`.
- Run with vanilla inventory, DadsEPI, and DadsCrafting.

## Build

```powershell
.\build.ps1
```

Create a validated Thunderstore package:

```powershell
.\build.ps1 -Package
```

See [THUNDERSTORE.md](THUNDERSTORE.md) for release checks.
