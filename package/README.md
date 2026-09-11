# DadsEZContainers

DadsEZContainers lets Valheim 1.0.12 crafting, building, cooking, fires, kilns, and smelters use resources stored in nearby containers.

## Features

- Counts nearby container contents in crafting and building requirements.
- Consumes each resource exactly once, taking inventory items before container items.
- Supports native Valheim containers and modded containers based on `Container`.
- Supports DadsCrafting batch quantities and DadsEPI inventory layouts.
- Pulls fuel, ore, and cookable items into processing stations.
- Configurable range and leave-one-item behavior.
- Per-container and per-station YAML include/exclude rules with reusable groups.
- Live rule-file reload and an in-game toggle shortcut.

## Configuration

General settings are written to `BepInEx/config/com.dadisbored.dadsezcontainers.cfg`. Pull rules are written to `BepInEx/config/Dad_Is_Bored.DadsEZContainers.yml`.

Default toggle: `LeftAlt + O`.

Source: https://github.com/DadxIsxBored/DadsEZContainers
