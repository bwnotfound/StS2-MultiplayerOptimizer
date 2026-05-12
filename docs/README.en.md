# StS2-MultiplayerOptimizer

English | [中文](../README.md)

A mod for *Slay the Spire 2* that adds extra acts and difficulty scaling for multiplayer runs.

## Overview

Two pain points the base game multiplayer has:

1. **Runs end after 3 acts**, the same as singleplayer, which feels too short for co-op;
2. **Difficulty is tuned for solo play**: multiple players hitting the same encounter pool without HP/damage scaling
   makes fights too easy.

This mod adds acts 4 and 5, exposes per-act HP / damage multipliers for both regular enemies and bosses, and ships a
full config-synchronization pipeline so every connected player runs with the host's settings.

## Features

### Custom Acts 4 and 5

- **Act 4**: an elite-only act. Every map node is forced to an elite icon. Encounter content is sampled from acts 1–3's
  elite pool with configurable per-source weighting.
- **Act 5**: a final-boss act. All middle combat nodes spawn boss-strength encounters; the top node is the real boss.
- Other supporting features: per-act ancient pool, event pool, treasure rooms, boss de-duplication, etc.

### Difficulty Multipliers (configurable per act)

- **Global multipliers**: regular-enemy HP / damage are interpolated linearly between "start" and "end" values across
  the act floors; boss HP / damage use a single scalar.
- **Per-source multipliers**: enemies are scaled based on which act they originally come from. For example, an act-1
  enemy seen in act 4 can be scaled by `1.4 × 1.8`, while an act-3 enemy in the same floor uses `1.4 × 1.0`.

### Weighted Pool Mixing

Act 4/5's encounter / event / boss / ancient pools draw from acts 1–3 based on user-configured weights. Weights are
auto-normalized to sum to 1 on save — no manual math needed.

### Automatic Config Sync (ack-based)

In multiplayer, **the host's mod configuration is automatically applied to all clients** for the duration of the run; on
run end each client reloads their own settings from disk. If a client's mod is too outdated or fails to acknowledge the
sync, the host shows a popup and the run is refused — preventing in-combat desync that would otherwise kick players.

## Installation

### Dependencies

- *Slay the Spire 2* base game
- [BaseLib](https://github.com/Alchyr/BaseLib-StS2) **v3.1.2** (exact version — base game compares mod version strings
  literally during multiplayer join)

### Steps

1. Extract `MultiplayerOptimizer/` into `<game root>/mods/`, containing:
    - `MultiplayerOptimizer.dll`
    - `MultiplayerOptimizer.pck`
    - `MultiplayerOptimizer.json`
2. Install BaseLib `v3.1.2` the same way
3. Launch the game → Settings → Mods → enable MultiplayerOptimizer and BaseLib

Verify the mod loaded by checking the game log's first MultiplayerOptimizer line:

```
[INFO] [MultiplayerOptimizer] [Init] Loading MultiplayerOptimizer version 0.3.0
```

## Configuration

Main menu → Settings → Mods → **MultiplayerOptimizer** → Configure

Sliders organized by category:

| Category                                                | Description                                                        |
|---------------------------------------------------------|--------------------------------------------------------------------|
| `Act4_EncWeights` / `EventWeights` / `BossWeights`      | Act 4 encounter / event / boss pool mixing weights from acts 1–3   |
| `Act4_NormalEnemyMultipliers`                           | Act 4 regular-enemy HP / damage (linear start → end across floors) |
| `Act4_BossMultipliers`                                  | Act 4 boss HP / damage scalar                                      |
| `Act4_NormalEnemySrcMultipliers` / `BossSrcMultipliers` | Per-source-act multipliers                                         |
| `Act5_*`                                                | Same shape, applied to act 5                                       |
| `BehaviorToggles`                                       | Act 5 boss-warning toggle, final-boss de-duplication toggle, etc.  |

**When all pool weights are set to 0**: the normalization logic falls back to defaults (
`Act1=0.25, Act2=0.35, Act3=0.40`) instead of dividing by zero.

## Multiplayer

### Important: All players must run the exact same mod version

The base game checks each connecting player's mod list by joining `<mod_id>-<version>` into strings and comparing
literally. Any difference — a missing `v` prefix, extra dot, mismatched whitespace — is treated as a ModMismatch and the
join is rejected.

**Recommended workflow**: the host zips up their entire `MultiplayerOptimizer/` folder and sends it to every player, who
**completely replaces** their own local copy.

### Sync Flow

```
Host clicks ready to begin run
  ↓
Host's mod broadcasts all config values to each client
  ↓ within 3s
Each client applies the values to its static fields, sends ack
  ↓
Host has all acks → invokes the original begin-run flow → game starts
              ↓ otherwise (timeout)
              Popup: "Mod version mismatch — please ask these players to update".
              Run does NOT start.
```

The sync happens during the lobby phase; players don't notice unless something fails (in which case there's a popup). On
run end every client reloads their own config from disk, so their personal preferences aren't corrupted.

### Troubleshooting

| Symptom                                              | Likely cause                                                                                                              |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Kicked from lobby with "Mod Mismatch" on join        | Players' manifest files differ (version string, or different set of installed mods)                                       |
| Host popup "Mod version mismatch" + run never starts | A client's mod isn't installed correctly or is too old; the sync message wasn't acked                                     |
| "State divergence, disconnected" mid-combat          | Host and client computed different results — typically the client doesn't have this mod enabled, or its sync didn't apply |

When the sync-failure popup shows, have the client reinstall the latest mod folder and restart the game.

## Known Issues

- **Loading a save with missing original players may hang on a black screen**: e.g. loading a 3-player save with only 2
  players present can deadlock the base game's `CombatStateSynchronizer`. **Workaround**: wait until everyone's online
  before loading, or start a fresh run.
- **No cross-version compatibility**: there's no protocol-level support for "I run new version, my friend runs old".
  Players must upgrade together.

## Changelog

| Version | Highlights                                                                                                                                  |
|---------|---------------------------------------------------------------------------------------------------------------------------------------------|
| 0.3.0   | ack-based config sync also covers `LoadRunLobby` (loading saves); version is now resolved at runtime from manifest (single source of truth) |
| 0.2.0   | Config sync became ack-based; host refuses to start the run with a popup when a client doesn't ack                                          |
| 0.1.0   | Initial release: acts 4–5, difficulty multipliers, fire-and-forget config broadcast                                                         |

## Feedback / Contributing

Bug reports and feature requests welcome
at [GitHub Issues](https://github.com/bwnotfound/StS2-MultiplayerOptimizer/issues).

Please include with your bug report:

- Mod version (first MultiplayerOptimizer line in the log)
- Reproduction steps
- Host's full godot.log
- If possible, the client's full godot.log

## Credits

- [Alchyr](https://github.com/Alchyr) for [BaseLib](https://github.com/Alchyr/BaseLib-StS2) and
  the [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2) template
- [GlitchedReme](https://github.com/GlitchedReme) for
  the [Chinese STS2 modding tutorials](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)