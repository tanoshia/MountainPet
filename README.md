# MountainPet v0.2.2

A cosmetic pet follower mod for Celeste. Adds a small axolotl companion that trails behind Madeline using the game's native follower system.

## What's new in v0.2.2

- Axolotl now supports 16 swim directions (N, NNE, NE, NEE, E, SEE, SE, SSE, S, SSW, SW, SWW, W, NWW, NW, NNW)
- 9 unique west-side direction sprites with FlipX mirroring for east-side
- Added randomize color toggle
- Multiple pet types (Axolotl, Goldfish) with data-driven registry

## Features

- Pet follows the player like keys/strawberries do
- Directional swimming animations (16 directions)
- Multiple pet types (Axolotl, Goldfish)
- Color variants per pet type (16 axolotl colors)
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
