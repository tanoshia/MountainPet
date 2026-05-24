# MountainPet v0.3.0

A cosmetic pet follower mod for Celeste. Adds a small axolotl companion that trails behind Madeline using the game's native follower system.

## What's new in v0.3.0

- Pet gets nudged slightly when overlapping or positioned behind the player
- Prevents visual clutter when standing still

## Features

- Pet follows the player like keys/strawberries do
- Directional swimming animations (16 directions)
- Multiple pet types (Axolotl, Goldfish)
- Color variants per pet type (16 axolotl colors)
- Overlap/behind nudge for cleaner visuals
- Always stays closest to the player in the follower chain
- Works on all levels (vanilla + modded) — no map edits needed

## Install

Drop this folder into `Celeste/Mods/` and restart the game.

## Build from source

Requires .NET 8 SDK. From inside `Celeste/Mods/MountainPet/`:

```bash
dotnet build Source/MountainPet.csproj
```

## License

MIT
