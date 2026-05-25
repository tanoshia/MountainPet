# MountainPet

A cosmetic pet follower mod for Celeste. Adds a small axolotl companion that trails behind Madeline using the game's native follower system. (It is a new entity, not a reskin of straberry/keys/gb etc. but uses the same mechanics). 
 
Inspired by the mod [Pet Strawberry](https://gamebanana.com/mods/435133), this uses the native strawberry follow mechanic instead of being player-distance locked.
 
All axolotl art/sprites designed by [Pop Shop Packs](https://pop-shop-packs.itch.io/)! 
 
## Features

- Pet follows the player (like keys/strawberries)
- Directional moving animations (up to 16 directions)
- Multiple pet types (Axolotl, Goldfish, Luma, Arrows)
  - *Checkout the github repo [here](https://github.com/tanoshia/MountainPet) on how to add custom sprites, add a new or replace Arrows*
- Color variants per pet type (16 axolotl colors, 7 luma colors) (or randomize)
- Smooth turning with arc offset
- Proximity nudge (pet avoids overlapping with player)
- Min follow distance (pet stays idle until player moves far enough away)
- Always stays closest to the player in the follower chain
- Works on all levels (vanilla + modded)

## Settings

In-game: Mod Options → MountainPet
- **Pet Enabled** — toggle on/off
- **Pet Type** — choose your pet (currently: Axolotl, Goldfish, Luma,or Arrows)
- **Pet Color** — choose color variant
- **Randomize Color** — random color each spawn (never repeats)
- **Pet Light** — ignore player's light source
- **Min Follow Distance** — how far the player must be before pet reacts (default 20px)
- **Experimental ▼**
  - Nudge Away From Player (on/off + trigger radius, offset, speed)
  - Smooth Turning (on/off + turn radius, turn speed)


---

## Adding a Custom Pet

### 1. Add your sprite folder

Place frames at:
```
Graphics/Atlases/Gameplay/objects/MountainPet/yourpet/
```

If you have color variants, use subfolders:
```
Graphics/Atlases/Gameplay/objects/MountainPet/yourpet/color_red/
Graphics/Atlases/Gameplay/objects/MountainPet/yourpet/color_blue/
```

Frame naming: `AnimationName` + frame number + `.png`
- `idle0.png`, `idle1.png` — idle loop
- `W0.png`, `W1.png`, `W2.png`, `W3.png` — swimming west loop
- `idle_to_W0.png`, `idle_to_W1.png` — transition (optional, one-shot)

You only need the directions your art covers. Missing directions fall back to the closest available (with FlipX for mirrored directions).

### 2. Add an entry to pets.json

Edit `Graphics/Atlases/Gameplay/objects/MountainPet/pets.json`:

```json
{
  "id": "yourpet",
  "name": "Your Pet",
  "folder": "yourpet",
  "facing": "west",
  "animations": {
    "idle": { "frames": "0-1", "delay": 0.5 },
    "W": { "frames": "0-3", "delay": 0.08 }
  },
  "colors": [
    { "folder": "color_red", "name": "Red" },
    { "folder": "color_blue", "name": "Blue" }
  ]
}
```

No rebuild needed — just add sprites, edit the JSON, and restart Celeste.

### Facing

Controls FlipX behavior:
- `"west"` — art faces left, flipped for east directions
- `"east"` — art faces right, flipped for west directions
- `"full"` — separate art for all directions, no flipping

### Optional: transition animation

Add `"idle_to_swim": { "path": "idle_to_W", "frames": "0-2", "delay": 0.06, "type": "oneshot" }` to play a one-shot animation before swimming starts.

> **Note:** `Source/PetRegistry.cs` contains a hardcoded fallback registry used if pets.json can't be loaded. You don't need to edit it unless you want your pet available even without the JSON file.

---

## Building

### From the Celeste Mods directory

Copy this folder into `Celeste/Mods/MountainPet/`, then from `Source/`:
```bash
dotnet build
```

### Windows (with auto-install)
```powershell
powershell -ExecutionPolicy Bypass -File build_windows.ps1
```

The script auto-installs .NET SDK if needed and builds the DLL.

### Creating a distributable zip
```powershell
.\build_windows.ps1 -Zip
```
Or build in Release mode: `dotnet build -c Release` (triggers the PackageMod target).

---

## Technical Notes

- Sprites are built programmatically at runtime (`PetRegistry.BuildSprite()`). No `Sprites.xml` needed.
- The mod uses Celeste's native `Leader`/`Follower` system for positioning.
- Position freeze overrides the follower system when the pet is within min follow distance.
- All visual effects (nudge, arc offset) are sprite offsets — the entity position follows the vanilla trail.

---

## Credits

- Axolotl art/sprites by [Pop Shop Packs](https://pop-shop-packs.itch.io/)
- Death animation adapted from [Pet Strawberry](https://gamebanana.com/mods/435133) by kuksa
