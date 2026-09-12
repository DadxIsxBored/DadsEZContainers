# Changelog

## 1.1.1

- Rebuilt against BepInEx `5.4.23.5` from BepInExPack Valheim `5.4.2350`.

## 1.1.0

- Added a configurable dropped-item pickup delay.
- Added a restricted range from `0.1` through `10.0` seconds.
- Added automatic one-decimal normalization for the pickup-delay value.

## 1.0.0

- Completed the AzuCraftyBoxes-compatible feature set for Valheim 1.0.12.
- Added current crafting, building, cooking, fermenter, fireplace, kiln, smelter, shield-generator, and turret integration.
- Embedded the optional compatibility APIs and YAML parser into one deployment DLL.
- Updated routed-RPC synchronization for the current Valheim API.
- Corrected the Thunderstore build pipeline to package the netstandard2.1 release assembly.

## 0.1.0

- Added nearby-container resource counting and atomic consumption for crafting and building.
- Added requirement UI totals and container-contribution coloring.
- Added nearby fuel, ore, and cooking-item staging.
- Added configurable range, leave-one behavior, pull rules, and local toggle shortcut.
- Added live reload for YAML pull rules.
