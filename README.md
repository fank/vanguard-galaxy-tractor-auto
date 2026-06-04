# Vanguard Galaxy Tractor Auto (VGTractorAuto)

A BepInEx 5 plugin for [Vanguard Galaxy](https://store.steampowered.com/app/3471800/) that lets your ship's **Manual Tractor Beams act as automatic beams too**, with the converted share scaling by your Autopilot (Engineering) skill-tree mastery.

A tractor module ships with two beam pools: a set of automatic beams that grab loose items on their own, and a set of *manual* beams that vanilla only ever uses when you hand-pick a target. This plugin progressively puts those manual beams to work automatically as your Autopilot mastery climbs — so a maxed commander auto-tractors with the whole module instead of leaving half the beams idle.

- **Mastery-scaled conversion.** The number of manual beams that auto-tractor = `floor(mastery / maxLevel × manualBeams)`. At 0 mastery nothing changes (pure vanilla); at the level cap, every manual beam auto-tractors.
- **Symmetric.** Hand-picking a manual target still works — it can claim any free beam, including an automatic one when the manual pool is busy.
- **Player ship only.** NPC and enemy ships keep vanilla behavior.
- **Respects vanilla limits.** Crew-pod targeting rules (e.g. skipping hostile pods when your brig is full) are honored exactly as vanilla does.
- **Transparency built in.** The Engineering ("Autopilot") mastery tooltip shows a live `Manual Tractor Beams act as automatic: N%` line, and an installed tractor module's tooltip notes that its manual beams also auto-tractor. Both are marked `(VGTractorAuto)`.

## Install

1. **Install BepInEx 5.x** — unzip `BepInEx_win_x64_5.4.x.zip` from the [BepInEx releases](https://github.com/BepInEx/BepInEx/releases) into your Vanguard Galaxy install folder (next to `VanguardGalaxy.exe`).
2. **Launch the game once** so BepInEx creates `BepInEx/plugins/` and `BepInEx/config/`, then close it.
3. **Download the VGTractorAuto release** zip from [Releases](https://github.com/fank/vanguard-galaxy-tractor-auto/releases).
4. **Unzip** into `BepInEx/plugins/`. The zip contains a single `VGTractorAuto/` folder that drops in cleanly:
   ```
   VanguardGalaxy/BepInEx/plugins/
     VGTractorAuto/
       VGTractorAuto.dll
   ```
5. **Launch the game.** The BepInEx console shows a load line, e.g.:
   ```
   [Info :Vanguard Galaxy Tractor Auto] Vanguard Galaxy Tractor Auto v0.2.0 loaded (4 patches)
   ```

## Uninstall

Delete the `BepInEx/plugins/VGTractorAuto/` folder. Optionally also delete `BepInEx/config/vgtractorauto.cfg` to reset saved settings.

## Config

BepInEx writes `BepInEx/config/vgtractorauto.cfg` on first launch:

| Key | Default | Purpose |
|---|---|---|
| `General.Enabled` | `true` | Master toggle. When `false`, manual beams revert to vanilla manual-only behavior and the transparency tooltip lines disappear. |

## How it works

Two HarmonyX postfixes on the game's tractor module, both gated on the player ship and the `Enabled` toggle:

- One lends a manual beam to auto-targeting once the automatic pool is full, capped at the mastery-scaled count (and, symmetrically, lets manual targeting borrow any free beam).
- One raises the auto-target candidate cap to match, so the extra beams engage in the same targeting cycle rather than trickling in.

Mastery is read live from the Autopilot (Engineering) skill tree and scaled against the current level cap, so the effect grows as you level and adapts to game modes with different caps. Two more postfixes add the disclosure tooltip lines. No save data is written; nothing is patched destructively.

## Build (for contributors)

The repository is a sibling of the other Vanguard Galaxy plugins and compiles against a committed **publicized** `Assembly-CSharp.dll` stub (method signatures only, no IL) at `VGTractorAuto/lib/` — the real game assembly takes over in-game via Mono late binding.

```sh
make build      # links libs, compiles VGTractorAuto.dll
make deploy     # copies into <GAME_DIR>/BepInEx/plugins/VGTractorAuto/
make clean
```

`<GAME_DIR>` is hard-coded to a WSL Steam path in the Makefile — adjust locally for non-WSL setups, but don't commit the change. There is no test project: the patched surface is live Unity components, so behavior is verified in-game.

## License

MIT — see [`LICENSE`](LICENSE).
