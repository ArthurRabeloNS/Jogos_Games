# Steve Cube — Minecraft in Overcooked! 2

A **BepInEx mod for Overcooked! 2** that transforms the **Player 1** chef into **Minecraft's Steve** and adds a mini Minecraft-style experience inside the kitchen: hotbar, blocks, TNT, Ender Pearl, fireworks rockets, camera zoom, and HUD.

> This is an unofficial, non-commercial fan project and is not affiliated with Mojang, Microsoft, Team17, or Ghost Town Games.
> Minecraft and Overcooked! are trademarks of their respective owners.

---

## What the mod does

- **Steve replaces the chef (Player 1)** — a blocky model built in code, using Steve's 64x64 skin and Minecraft-style animations (walking, breathing, and arm swings while chopping or washing).
- **Tools in hand** — when the chef chops, Steve holds a sword, axe, or pickaxe.
- **Floating items in hand** — the ingredient you're carrying spins and floats near Steve's hand.
- **9-slot hotbar** with heart, armor, hunger, and XP bar HUD elements (visual only).
- **Blocks** — place wood and obsidian, break blocks, and even break map countertops.
- **TNT** — a 4-second fuse, blinking, and an explosion.
- **Ender Pearl** — throw it to teleport.
- **Rocket** — launches and bursts into colorful fireworks.
- **Camera** — zoom in and out, and optionally make the camera follow Steve.

Player 2 keeps the original chef.

---

## Requirements

- **Overcooked! 2** (Steam, PC)
- **[BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases)** (Unity Mono) installed in the game

---

## Installation (for playing)

1. **Install BepInEx 5** for Overcooked! 2:
   - Download BepInEx 5 (the version matching your game, x86 or x64) from the [releases page](https://github.com/BepInEx/BepInEx/releases).
   - Extract **all files into the game folder**, next to `Overcooked2.exe`.
   - Example: `C:\Program Files (x86)\Steam\steamapps\common\Overcooked! 2\`
2. **Launch the game once, then close it.** This lets BepInEx create folders such as `BepInEx\plugins`, `BepInEx\config`, and others.
3. **Copy `SteveCube.dll`** (from the *Releases* section of this repository) into:
   ```
   Overcooked! 2\BepInEx\plugins\
   ```
4. Launch the game and enter a level. Steve should appear in place of Player 1.

> **Custom skin (optional):** Steve's skin is already embedded in the DLL. To use another skin, place a **64x64 PNG** (Minecraft skin format; 64x32 also works) named `steve.png` in `BepInEx\plugins\`. This file takes priority over the embedded skin.

---

## Controls

| Key | Action |
|---|---|
| `1`–`9` or **mouse wheel** | Select a hotbar slot |
| `B` | Place the selected slot's item in front of Steve (slots 1, 2, 5, and 6) |
| `U` | Break the block or countertop in front of Steve |
| `N` | Restore all broken countertops |
| `T` | Throw the Ender Pearl (with slot 3 selected) |
| `=` (hold) / numpad `+` | Zoom in |
| `-` (hold) / numpad `-` | Zoom out |
| `0` | Reset zoom |
| `V` | Toggle the camera-following-Steve mode |

### Hotbar

| Slot | Item | How to use |
|---|---|---|
| 1 | Wood | Press `B` to place a solid wood block |
| 2 | TNT | Press `B` to place it; explodes after 4 seconds |
| 3 | Ender Pearl | Press `T` to throw it and teleport Steve |
| 4 | Pickaxe | Visual item |
| 5 | Obsidian | Press `B` to place it; **TNT cannot break it** — only `U` can |
| 6 | Rocket | Press `B` to place it; launches and turns into fireworks |
| 7–9 | In-game items | Automatically filled with ingredients you pick up; the slot becomes available again when you drop or deliver the item |

Keys and values can be changed in the configuration files (see below).

---

## Configuration

After launching the game once with the mod installed, configuration files are created in `BepInEx\config\`:

| File | Contents |
|---|---|
| `com.seunome.oc2.stevecube.cfg` | Steve's size and position, animation, held item, and debugging |
| `com.seunome.oc2.steveblocks.cfg` | B/U/N keys, block size, interaction range, and what can be broken |
| `com.seunome.oc2.steveround2.cfg` | Camera (zoom, follow mode, keys) and HUD |

Main options:

**Steve** (`stevecube.cfg`)
- `AlturaMundo` — Steve's height in the game (default `1.5`)
- `LarguraBlocos` — `1` matches Minecraft's exact proportions; increase it to make Steve wider
- `AjusteY` — moves Steve up or down if his feet sink into the floor or float above it
- `RotacaoY` — set to `180` if Steve appears facing away
- `VelocidadeDaCaminhada`, `FerramentasNaMao`, `EscalaNaMao`, `GiroGrausPorSeg`
- `OcultarPorLayer` — fallback option: enable this if the original chef **still appears** alongside Steve

**Blocks** (`steveblocks.cfg`)
- `ColocarBloco`, `Quebrar`, `RestaurarBancadas` — key bindings
- `TamanhoDoBloco`, `AlcanceParaQuebrar`, `LimiteDeBlocos` (the oldest block disappears after the limit is reached; default is 64)
- `QuebrarBancadas` (enabled by default), `QuebrarEstacoes` (disabled by default; **may freeze the level**), `ProtegerBordas`, `MargemDaBorda`

**Camera and HUD** (`steveround2.cfg`)
- `Zoom` (0 = original camera, 1 = very close), `SeguirSteve`, and zoom keys
- HUD: `Escala` (0 = automatic), `MostrarVidaFomeXP`, `MostrarNumeros`

---

## How it works (technical overview)

The project consists of three BepInEx plugins inside a single DLL:

| File | Plugin | Purpose |
|---|---|---|
| `Plugin.cs` | **Steve Cube** | Builds and animates Steve |
| `Blocks.cs` | **Steve Blocks** | Places and breaks blocks |
| `Round2.cs` | **Steve Cube – Round 2** | Hotbar, HUD, camera, TNT, Ender Pearl, and rocket |
| `Skin.cs` | — | Steve's skin encoded in Base64 and embedded in the DLL |
| `HudArt.cs` | — | Pixel-art HUD atlas encoded in Base64 (hearts, hunger icons, and slot icons) |

**Steve (`Plugin.cs`)**
1. Every 0.5 seconds, the plugin searches the scene for the `Chefs` object and finds `Player 1`.
2. It creates a six-box model (head, body, two arms, and two legs), with UVs matching Minecraft's skin layout, and positions it over the chef.
3. It **hides the original chef** in several ways at once: `forceRenderingOff`, `enabled = false` every frame and before each camera render (`Camera.onPreCull` / `onPostRender`), and optionally moving the body to a layer that no camera renders.
4. It reads the original chef's state (walking, chopping, washing, and holding an item) and converts it into Steve animations using Minecraft-style walking and arm-swing formulas.
5. The game's item attachment point (where carried items appear) is moved to Steve's hand.

**Blocks (`Blocks.cs`)**
- Blocks are aligned to a grid and have collision. `U` only breaks Steve's blocks and objects inside `Work Surfaces` (and `Gameplay Stations`, if enabled). Walls, floors, and map boundaries can never be broken.

**Round 2 (`Round2.cs`)**
- The hotbar only controls the **visibility** of the held item. Ingredients remain controlled by Overcooked.
- The game's camera remains in control: the mod only offsets its position immediately before rendering and restores it afterward, so the game's scripts do not notice.

**No extra files required:** the skin, TNT/obsidian textures, and HUD artwork are generated by code or embedded as Base64 in the DLL.

---

## Building from source

Requirements: **.NET SDK** (any recent version) and the game installed.

1. Clone the repository.
2. Open `SteveCube.csproj` and **change the game path** on this line:
   ```xml
   <OC2>D:\SteamLibrary\steamapps\common\Overcooked! 2</OC2>
   ```
   to the folder where your copy of Overcooked! 2 is installed.
3. Build the project:
   ```
   dotnet build -c Release
   ```
4. The build process **automatically copies** `SteveCube.dll` to the game's `BepInEx\plugins` folder.

The project references the game's Unity DLLs (`Overcooked2_Data\Managed`) and `BepInEx.dll` (`BepInEx\core`), so BepInEx must be installed before building. Target framework: `net35`, C# 7.3.

---

## Troubleshooting

The log file is located at `BepInEx\LogOutput.log`.

| Problem | What to do |
|---|---|
| Nothing happens | Make sure BepInEx is installed (the `BepInEx` folder exists and the log is created when launching the game) and `SteveCube.dll` is in `BepInEx\plugins`. |
| The original chef appears alongside Steve | In `stevecube.cfg`, enable `OcultarPorLayer`. |
| Steve is facing away | Set `RotacaoY = 180`. |
| Steve's feet sink into the floor or float | Adjust `AjusteY`. |
| Steve is too thin, wide, tall, or short | Adjust `LarguraBlocos` and `AlturaMundo`. |
| Steve appears pink, black, or untextured | Check the log to see which shader was selected; the mod automatically searches for a compatible opaque shader. |
| The level freezes after breaking something | Disable `QuebrarEstacoes` and press `N` to restore the countertops. |
| I want to inspect the chef's hierarchy | In `stevecube.cfg`, enable `GerarDump`. The mod writes `BepInEx\chef_dump2.txt` with the chef hierarchy. |

---

## Disclaimer

This mod is intended for offline/local use and entertainment. It may break after game updates. Use it at your own risk.

## Credits

- Created by **ARTHUR RABELO NUNES**
- [BepInEx](https://github.com/BepInEx)

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE.txt) for details.
