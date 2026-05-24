# MountainPet

A cosmetic pet follower mod for Celeste. Adds a small companion that trails behind Madeline using the game's native follower system.

## Features

- Pet follows the player like keys/strawberries do
- Directional swimming animations (8 directions)
- Multiple pet types (Axolotl, Goldfish)
- Color variants per pet type (16 axolotl colors)
- Always stays closest to the player in the follower chain
- Works on all levels (vanilla + modded) — no map edits needed

## Settings

In-game: Mod Options → MountainPet
- **Pet Enabled** — toggle on/off
- **Pet Type** — choose your pet
- **Pet Color** — choose color variant (changes require level reload)

---

## Adding a Custom Pet

Two files to change:

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

Supported animation IDs: `idle`, `rest`, `N`, `S`, `W`, `NW`, `SW`, `E`, `NE`, `SE`, `idle_to_W`

You only need the directions your art covers. Missing directions fall back to the closest available.

### 2. Register in `Source/PetRegistry.cs`

Add a method like `RegisterFish()` and call it from the static constructor:

```csharp
private static void RegisterYourPet() {
    AllPets.Add(new PetTypeInfo {
        Id = "yourpet",              // Internal ID
        Name = "Your Pet",           // Display name in menu
        Folder = "yourpet",          // Folder name under objects/MountainPet/
        Facing = "west",             // "west", "east", or "full"
        HasTransition = false,       // true if you have idle_to_W frames
        TransitionAnimPath = null,   // "idle_to_W" if HasTransition is true
        Animations = new HashSet<string> { "idle", "W" },
        AnimationDefs = new Dictionary<string, AnimationDef> {
            ["idle"] = new() { Path = "idle", Frames = new[] {0, 1}, Delay = 0.5f },
            ["W"] = new() { Path = "W", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
        },
        Colors = new List<PetColorInfo> {
            new() { Folder = "color_red", Name = "Red" },
            new() { Folder = "color_blue", Name = "Blue" },
        }
    });
}
```

**Facing** controls FlipX behavior:
- `"west"` — art faces left, flipped for east directions
- `"east"` — art faces right, flipped for west directions
- `"full"` — separate art for all directions, no flipping

Then rebuild with `dotnet build`.
