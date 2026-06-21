# StS2-NotEnoughDifficulty

English | [中文](../README.md)

A mod for *Slay the Spire 2* that expands the original 3-act tower into a **5-act tower** and adds a full suite of *
*per-act difficulty, map, and enemy-pool** controls. Works in both single-player and multiplayer; in multiplayer the
host's configuration is synced to every player.

> Current version **1.0.1**. Migrated to the latest game build and rebuilt on **BaseLib v3.3.0**'s registration-based
> localization.

---

## Overview

Two pain points in the base game (especially multiplayer):

1. **It ends after three acts** — runs feel short.
2. **Difficulty is tuned for solo** — when several players gang up on the same enemies, the challenge is too low.

This mod adds **Act 4 (all elites)** and **Act 5 (with the final boss)** after the base 3 acts, plus per-act HP/damage
multipliers, difficulty presets, map-length control, an enemy removal list, an extra speed mode, and more. In
multiplayer, ack-based config sync ensures all players start the run with the host's settings.

---

## Features

### 1. Custom Acts 4 / 5

- **Act 4**: an all-elite act. Every map node is forced to an elite-combat icon; encounter content is mixed from the
  elite pools of acts 1–3 by configurable weights.
- **Act 5**: an act containing the final boss. Mid-act combat nodes use boss-strength fights, with the real final boss
  at the top.
- Each act has independent ancient / event pools, treasure rooms, and rest sites; bosses don't repeat across earlier
  acts.

### 2. Difficulty Multipliers (per act)

- **Overall**: a single HP / damage knob per act, applied on top of everything else — the most direct way to tune
  overall difficulty.
- **Global**: regular-enemy HP/damage can interpolate linearly from "start → end" over act progress; bosses use a single
  multiplier.
- **Source (Src)**: an additional multiplier based on the enemy's origin act (act 1/2/3) — e.g. act-1 enemies and act-3
  enemies encountered in Act 4 can be scaled differently.

### 3. Difficulty Presets (one-click)

Three preset buttons — **Easy / Hard / Extreme** — set the Act 4/5 overall HP/damage multipliers in one click (only the
four Overall values; weights and detailed settings are left untouched, so behavior is predictable). Preset values are
defined centrally in code for easy balance tweaking.

### 4. Map Length & Room Density

- Each act's map length (number of rows) is independently configurable (defaults: act1=16 / act2=15 / act3/4/5=14,
  matching vanilla).
- Gated behind a **master toggle (off by default)**: length changes only apply when enabled, and the per-act sliders are
  hidden while disabled — so players don't change the map structure unknowingly.
- When length increases, the density of special rooms (elite / rest / unknown) is **scaled accordingly** so the map
  isn't diluted by normal fights; very long maps also **skip the exponential path-pruning** step for performance.

### 5. Enemy Removal List

- A custom popup (opened from the "Manage" button in the config page) scans the game's registered **normal / elite /
  boss** encounters and offers three dropdowns to add enemies to a removal list, with add/remove support and fallbacks
  for duplicate add/remove and pool exhaustion.
- Enemy names in the dropdowns/list carry a **layer suffix** (e.g. `(Act 1)`) to help gauge each pool's size; entries
  that can't be mapped to a layer are tagged `(Other)`.
- Includes a **scope toggle**: defaults to "all acts (1–5)", optionally switchable to "only Acts 4–5".
- Removed enemies are excluded from the corresponding act's draw (base acts use replacement-based filtering to avoid
  emptying a pool and failing to spawn fights).

### 6. Extra Speed Mode

- Injects two rows (enable toggle + multiplier slider) into the **game's official settings screen**, kept **value-synced
  ** with the mod config screen.
- Provides combat/animation speed multipliers beyond the vanilla cap for faster runs.

### 7. Pool Weight Mixing

Act 4/5 encounter / event / boss / ancient pools are mixed from acts 1–3 by user-configured weights. Weights are
auto-normalized on save (sum = 1); all-zero falls back to defaults to avoid division by zero.

### 8. Multiplayer Config Sync (ack-based)

In multiplayer, the host's full mod config is automatically synced to all clients for the duration of the run; clients
restore their local config from disk afterward. If a client is too old or sync fails, the host shows a popup and refuses
to start the run, avoiding mid-combat value mismatches.

### 9. Save-Load Robustness

For multi-mod setups (where another mod drops data while handling the extra acts on the `FromSerializable` chain), a
defensive guard fills null room id-lists on load to avoid a hard crash, and logs diagnostics for the affected act.

---

## Installation

### Dependencies

- *Slay the Spire 2* base game
- [BaseLib](https://github.com/Alchyr/BaseLib-StS2) **v3.3.0** (strict version — multiplayer validates the exact mod
  version string)

### Steps

1. Extract the `NotEnoughDifficulty/` folder into `<game root>/mods/`, ensuring it contains:
    - `NotEnoughDifficulty.dll`
    - `NotEnoughDifficulty.pck`
    - `NotEnoughDifficulty.json`
2. Install BaseLib **v3.3.0** the same way.
3. Launch the game, Main Menu → Settings → Mods, and enable NotEnoughDifficulty and BaseLib.

Confirm a successful load in the log (the version is read from the manifest at runtime to avoid drift between code and
json):

```
[INFO] [NotEnoughDifficulty] Loading NotEnoughDifficulty 1.0.1
```

---

## Configuration

Main Menu → Settings → Mods → **NotEnoughDifficulty** → Configure. Settings are grouped into sections:

| Section                                                                 | Description                                                                                          |
|-------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `General`                                                               | Master toggle (enable/disable the whole difficulty suite)                                            |
| `Presets`                                                               | Easy / Hard / Extreme one-click preset buttons                                                       |
| `Act4Act5Scaling`                                                       | Collapse toggle controlling whether the Act 4/5 detail rows are shown, to avoid information overload |
| `Act4_OverallMultipliers` / `Act5_OverallMultipliers`                   | Per-act overall HP / damage multipliers                                                              |
| `Act4_NormalEnemyMultipliers` / `Act5_NormalEnemyMultipliers`           | Regular-enemy HP/damage (linear start→end over act progress)                                         |
| `Act4_BossMultipliers` / `Act5_FinalBossMultipliers`                    | Boss / final-boss HP/damage multipliers                                                              |
| `Act4_NormalEnemySrcMultipliers` / `Act4_BossSrcMultipliers`, etc.      | Per-origin-act multipliers (normal / boss, one set each for acts 4/5)                                |
| `Act4_EncWeights` / `Act4_EventWeights` / `Act4_BossWeights` / `Act5_*` | Mixing weights from acts 1–3 for each pool                                                           |
| `MapLength`                                                             | Map-length master toggle + per-act row sliders                                                       |
| `RemovalList`                                                           | Enemy removal list entry ("Manage" button opens the popup)                                           |
| `Speed`                                                                 | Extra speed multiplier (synced with the two injected rows in the game settings screen)               |
| `BehaviorToggles`                                                       | Behavior toggles like act5 boss warning, final-boss dedupe                                           |
| `Experimental`                                                          | Experimental options                                                                                 |

> **When a pool weight is set to 0**: normalization falls back to defaults (`Act1=0.25, Act2=0.35, Act3=0.40`) to avoid
> division by zero.

---

## Multiplayer

### Important: all players must run the exact same mod version

The base game validates the peers' mod lists by concatenating `<mod_id>-<version>`; any character mismatch (including a
`v` prefix or dot placement) is treated as a ModMismatch and rejected.

**Safest approach**: the host packages the entire mod folder and sends it to everyone, who **completely replace** their
local `NotEnoughDifficulty/` directory (along with the same BaseLib version).

### Config sync flow

```
host clicks ready to start the run
  ↓ host broadcasts all config to clients
  ↓ ≤ 3s
all clients receive → apply to local static fields → ack
  ↓ host collects all acks → original begin-run flow → into combat
  ↓ otherwise
  popup "Mod version incompatible, ask these players to upgrade", run does not start
```

Sync happens during the lobby phase and is invisible to players (unless an error popup appears). After the run, clients
reload their own config from disk, leaving local settings untouched.

### Troubleshooting

| Symptom                                                   | Likely cause                                                                                      |
|-----------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| Kicked with "Mod Mismatch" on joining the lobby           | Manifests differ between players (different version string / an extra or missing mod)             |
| Host popup "Mod version incompatible" + run doesn't start | A client isn't installed correctly or is too old; sync got no ack                                 |
| "State divergence, disconnected" during combat            | Host/client results diverge — usually a client without the mod enabled or sync didn't take effect |

If you hit a sync-failure popup, have the client reinstall the latest mod folder and restart the game.

---

## Project Structure (after the migration/refactor)

All code lives under `NotEnoughDifficultyCode/`, organized by feature:

| Directory            | Responsibility                                                                                                                                                                             |
|----------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Core/`              | Entry point `MainFile`, per-class isolated patching, the `PatchScope` master switch, run-state access                                                                                      |
| `Config/`            | `NotEnoughDifficultyConfig` (split into partials per section) + the `ExtraActsConfig` logic layer                                                                                          |
| `ExtraActs/`         | Acts 4/5: `Bootstrap` (inject the act list), `Models` (Act4/5Model), `Patches` (encounter replacement / dedupe / map nodes), `Pool` (mixing/dedupe utils), `Compat` (compatibility guards) |
| `Difficulty/`        | Runtime HP/damage multiplier application, desync diagnostics                                                                                                                               |
| `MapLength/`         | Map-length patches, density scaling, skip path-pruning for long maps                                                                                                                       |
| `RemovalList/`       | Enemy removal list popup UI                                                                                                                                                                |
| `SpeedControl/`      | Extra speed multiplier control                                                                                                                                                             |
| `SettingsInjection/` | Injects the two speed rows into the game's settings screen (synced with config)                                                                                                            |
| `MultiplayerSync/`   | Ack-based config sync, deterministic model hashing                                                                                                                                         |
| `Act5/`              | Act-5 mid-boss flow/reward/dedupe patches                                                                                                                                                  |
| `SaveCompat/`        | `COMPAT-PRELAUNCH` inventory of legacy save-compat code (documentation directory)                                                                                                          |

---

## Known Issues

- **Load crash in multi-mod setups**: some mods drop room data while handling this mod's extra acts on the
  `FromSerializable` chain, causing an `ArgumentNullException` on load. This mod adds a defensive guard to avoid a hard
  crash, but if the affected act is the current one there may be follow-up issues — please report the logs (see what
  `ExtraActs/Compat/RoomSetLoadNullGuardPatch.cs` prints).
- **Loading an under-populated multiplayer save may hang on a black screen**: e.g. loading a 3-player save with only 2
  players online can deadlock the base game's `CombatStateSynchronizer`. Workaround: wait for all original players, or
  start a new run.
- **Different mod versions are incompatible**: after upgrading, teammates must upgrade in sync; there is no
  protocol-level backward compatibility.

---

## Version History

| Version | Highlights                                                                                                                                                                                                                                                                                                         |
|---------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1.0.1   | Guard against a divide-by-zero crash on loading into the first Act 5 combat (RoomSet.NextNormalEncounter): deterministically rebuild normal/elite/event/boss from act pools when a mod act's save data is lost; defensive hardening across patches for coexistence with other mods that patch the same game source |
| 1.0.0   | First official Steam Workshop release; fixed ConfigSync not syncing string fields (removal list); added workshop publish flow (csproj publishes to ModUploader content)                                                                                                                                            |
| 0.7.0   | Migrated to the latest game build + BaseLib v3.3.0 (registration-based localization); added difficulty presets, map length/density control, enemy removal list (with layer suffixes and a scope toggle), extra speed mode, save-load robustness; directory & config refactor                                       |
| 0.4–0.6 | Difficulty system expansion (overall/source multipliers, per-act detail), collapsible config UI, various compatibility fixes (incremental)                                                                                                                                                                         |
| 0.3.0   | Ack-based config sync also on the LoadRunLobby path; version read from the manifest at runtime                                                                                                                                                                                                                     |
| 0.2.0   | ConfigSync switched to ack-based; host refuses to start the run + popup when a client doesn't respond                                                                                                                                                                                                              |
| 0.1.0   | Initial release: basic Act 4/5 + config broadcast (fire-and-forget)                                                                                                                                                                                                                                                |

> 0.4–0.6 were incremental; no precise per-version changelog was kept, so the table summarizes that range.

---

## Feedback / Contributing

Report bugs and suggest features at [GitHub Issues](https://github.com/bwnotfound/StS2-NotEnoughDifficulty/issues). When
reporting a bug, please include:

- the mod version (first log line)
- reproduction steps
- the full host `godot.log` (and the client's if possible)

---

## Credits

- [Alchyr](https://github.com/Alchyr)'s [BaseLib](https://github.com/Alchyr/BaseLib-StS2)
  and [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)
- [GlitchedReme](https://github.com/GlitchedReme)'
  s [Chinese StS2 modding tutorials](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials)