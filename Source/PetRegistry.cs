using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Monocle;

namespace Celeste.Mod.MountainPet;

public class PetTypeInfo {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Folder { get; set; }
    public string Facing { get; set; }
    public HashSet<string> Animations { get; set; }
    public Dictionary<string, AnimationDef> AnimationDefs { get; set; }
    public List<PetColorInfo> Colors { get; set; }
    public bool HasTransition { get; set; }
    public string TransitionAnimPath { get; set; }
}

public class AnimationDef {
    public string Path { get; set; }
    public int[] Frames { get; set; }
    public float Delay { get; set; }
    public bool IsOneShot { get; set; }
}

public class PetColorInfo {
    public string Folder { get; set; }
    public string Name { get; set; }
}

/// <summary>
/// Registry of all available pet types.
/// To add a new pet: add an entry here + add the sprite folder. That's it.
/// </summary>
public static class PetRegistry {
    private static readonly List<PetTypeInfo> AllPets = new();
    private static bool loaded = false;

    /// <summary>
    /// Loads pet definitions from pets.json. Falls back to hardcoded data if file not found.
    /// Must be called from MountainPetModule.Load().
    /// </summary>
    public static void Load() {
        if (loaded) return;
        loaded = true;

        try {
            string modDir = FindModDirectory();
            if (modDir != null) {
                string jsonPath = Path.Combine(modDir, "Graphics", "Atlases",
                    "Gameplay", "objects", "MountainPet", "pets.json");

                if (File.Exists(jsonPath)) {
                    string json = File.ReadAllText(jsonPath);
                    ParsePetsJson(json);
                    Logger.Log(LogLevel.Info, "MountainPet",
                        $"Loaded {AllPets.Count} pet types from pets.json at {jsonPath}");
                    return;
                }

                Logger.Log(LogLevel.Warn, "MountainPet",
                    $"pets.json not found at {jsonPath}, using hardcoded fallback.");
            } else {
                Logger.Log(LogLevel.Warn, "MountainPet",
                    "Could not determine mod directory, using hardcoded fallback.");
            }
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, "MountainPet",
                $"Failed to parse pets.json: {e.Message}. Using hardcoded fallback.");
        }

        LoadFallback();
    }

    private static string FindModDirectory() {
        // Method 1: Check Everest module metadata
        foreach (var mod in Everest.Modules) {
            if (mod is MountainPetModule && mod.Metadata?.PathDirectory != null) {
                return mod.Metadata.PathDirectory;
            }
        }

        // Method 2: Assembly location (may be empty on .NET 8)
        string asmPath = typeof(PetRegistry).Assembly.Location;
        if (!string.IsNullOrEmpty(asmPath)) {
            // DLL at .../Mods/MountainPet/bin/MountainPet.dll → up 2 levels
            return Path.GetDirectoryName(Path.GetDirectoryName(asmPath));
        }

        // Method 3: Search Mods directory for our folder
        string celesteDir = Path.GetDirectoryName(typeof(Celeste).Assembly.Location);
        if (!string.IsNullOrEmpty(celesteDir)) {
            string modsDir = Path.Combine(celesteDir, "Mods", "MountainPet");
            if (Directory.Exists(modsDir))
                return modsDir;
        }

        return null;
    }

    private static void ParsePetsJson(string json) {
        AllPets.Clear();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("pets", out var petsArray))
            return;

        foreach (var petEl in petsArray.EnumerateArray()) {
            var pet = new PetTypeInfo {
                Id = petEl.GetProperty("id").GetString(),
                Name = petEl.GetProperty("name").GetString(),
                Folder = petEl.GetProperty("folder").GetString(),
                Facing = petEl.GetProperty("facing").GetString(),
                Animations = new HashSet<string>(),
                AnimationDefs = new Dictionary<string, AnimationDef>(),
                Colors = new List<PetColorInfo>(),
                HasTransition = false,
                TransitionAnimPath = null
            };

            if (petEl.TryGetProperty("animations", out var animsEl)) {
                foreach (var animProp in animsEl.EnumerateObject()) {
                    string animId = animProp.Name;
                    var animVal = animProp.Value;

                    string path = animId;
                    if (animVal.TryGetProperty("path", out var pathEl))
                        path = pathEl.GetString();

                    int frameStart = 0, frameEnd = 0;
                    if (animVal.TryGetProperty("frames", out var framesEl)) {
                        string frames = framesEl.GetString();
                        if (frames.Contains('-')) {
                            var parts = frames.Split('-');
                            frameStart = int.Parse(parts[0]);
                            frameEnd = int.Parse(parts[1]);
                        } else {
                            frameStart = int.Parse(frames);
                            frameEnd = frameStart;
                        }
                    }

                    float delay = 0.1f;
                    if (animVal.TryGetProperty("delay", out var delayEl))
                        delay = (float)delayEl.GetDouble();

                    bool isOneShot = false;
                    if (animVal.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "oneshot")
                        isOneShot = true;

                    // Build frames array
                    int count = frameEnd - frameStart + 1;
                    int[] frameArray = new int[count];
                    for (int i = 0; i < count; i++)
                        frameArray[i] = frameStart + i;

                    pet.AnimationDefs[animId] = new AnimationDef {
                        Path = path,
                        Frames = frameArray,
                        Delay = delay,
                        IsOneShot = isOneShot
                    };
                    pet.Animations.Add(animId);

                    if (animId == "idle_to_swim" || isOneShot) {
                        pet.HasTransition = true;
                        pet.TransitionAnimPath = path;
                    }
                }
            }

            if (petEl.TryGetProperty("colors", out var colorsEl)) {
                foreach (var colorEl in colorsEl.EnumerateArray()) {
                    pet.Colors.Add(new PetColorInfo {
                        Folder = colorEl.GetProperty("folder").GetString(),
                        Name = colorEl.GetProperty("name").GetString()
                    });
                }
            }

            if (pet.Colors.Count == 0)
                pet.Colors.Add(new PetColorInfo { Folder = "", Name = "Default" });

            AllPets.Add(pet);
        }
    }

    private static void LoadFallback() {
        AllPets.Clear();
        RegisterAxolotl();
        RegisterFish();
        RegisterDebugArrows();
        RegisterLuma();
    }

    private static void RegisterAxolotl() {
        AllPets.Add(new PetTypeInfo {
            Id = "axolotl",
            Name = "Axolotl",
            Folder = "axolotl",
            Facing = "west",
            HasTransition = true,
            TransitionAnimPath = "idle_to_W",
            Animations = new HashSet<string> { "idle", "rest", "idle_to_W", "N", "NNW", "NW", "NWW", "W", "SWW", "SW", "SSW", "S" },
            AnimationDefs = new Dictionary<string, AnimationDef> {
                ["idle"] = new() { Path = "idle", Frames = new[] {0, 1}, Delay = 0.5f },
                ["rest"] = new() { Path = "rest", Frames = new[] {0, 1, 2, 3}, Delay = 0.15f },
                ["idle_to_W"] = new() { Path = "idle_to_W", Frames = new[] {0, 1, 2}, Delay = 0.06f, IsOneShot = true },
                ["N"] = new() { Path = "N", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["NNW"] = new() { Path = "NNW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["NW"] = new() { Path = "NW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["NWW"] = new() { Path = "NWW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["W"] = new() { Path = "W", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["SWW"] = new() { Path = "SWW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["SW"] = new() { Path = "SW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["SSW"] = new() { Path = "SSW", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
                ["S"] = new() { Path = "S", Frames = new[] {0, 1, 2, 3}, Delay = 0.08f },
            },
            Colors = new List<PetColorInfo> {
                new() { Folder = "tiny_axolotl_pink", Name = "Pink" },
                new() { Folder = "tiny_axolotl_albino", Name = "Albino" },
                new() { Folder = "tiny_axolotl_black", Name = "Black" },
                new() { Folder = "tiny_axolotl_blue00", Name = "Blue" },
                new() { Folder = "tiny_axolotl_blue01", Name = "Sky Blue" },
                new() { Folder = "tiny_axolotl_brown", Name = "Brown" },
                new() { Folder = "tiny_axolotl_dark-orange", Name = "Dark Orange" },
                new() { Folder = "tiny_axolotl_dark-purple", Name = "Dark Purple" },
                new() { Folder = "tiny_axolotl_greyscale", Name = "Greyscale" },
                new() { Folder = "tiny_axolotl_red", Name = "Red" },
                new() { Folder = "tiny_axolotl_retrogreen", Name = "Retro Green" },
                new() { Folder = "tiny_axolotl_rose-pink", Name = "Rose Pink" },
                new() { Folder = "tiny_axolotl_swamp-green", Name = "Swamp Green" },
                new() { Folder = "tiny_axolotl_tan", Name = "Tan" },
                new() { Folder = "tiny_axolotl_yellow", Name = "Yellow" },
                new() { Folder = "tiny_axolotl_yellow-green", Name = "Yellow Green" },
            }
        });
    }

    private static void RegisterFish() {
        AllPets.Add(new PetTypeInfo {
            Id = "fish",
            Name = "Goldfish",
            Folder = "fish",
            Facing = "east",
            HasTransition = false,
            TransitionAnimPath = null,
            Animations = new HashSet<string> { "idle", "E", "NE", "SE" },
            AnimationDefs = new Dictionary<string, AnimationDef> {
                ["idle"] = new() { Path = "NE", Frames = new[] {0}, Delay = 0.5f },
                ["E"] = new() { Path = "E", Frames = new[] {0}, Delay = 0.08f },
                ["NE"] = new() { Path = "NE", Frames = new[] {0}, Delay = 0.08f },
                ["SE"] = new() { Path = "SE", Frames = new[] {0}, Delay = 0.08f },
            },
            Colors = new List<PetColorInfo> {
                new() { Folder = "fish_red", Name = "Red" },
                new() { Folder = "fish_blue", Name = "Blue" },
            }
        });
    }

    private static void RegisterDebugArrows() {
        AllPets.Add(new PetTypeInfo {
            Id = "debug_arrows",
            Name = "Debug Arrows",
            Folder = "debug_arrows",
            Facing = "east",
            HasTransition = false,
            TransitionAnimPath = null,
            Animations = new HashSet<string> { "idle", "N", "NNE", "NE", "NEE", "E", "SEE", "SE", "SSE", "S" },
            AnimationDefs = new Dictionary<string, AnimationDef> {
                ["idle"] = new() { Path = "E", Frames = new[] {0}, Delay = 0.5f },
                ["N"] = new() { Path = "N", Frames = new[] {0}, Delay = 0.08f },
                ["NNE"] = new() { Path = "NNE", Frames = new[] {0}, Delay = 0.08f },
                ["NE"] = new() { Path = "NE", Frames = new[] {0}, Delay = 0.08f },
                ["NEE"] = new() { Path = "NEE", Frames = new[] {0}, Delay = 0.08f },
                ["E"] = new() { Path = "E", Frames = new[] {0}, Delay = 0.08f },
                ["SEE"] = new() { Path = "SEE", Frames = new[] {0}, Delay = 0.08f },
                ["SE"] = new() { Path = "SE", Frames = new[] {0}, Delay = 0.08f },
                ["SSE"] = new() { Path = "SSE", Frames = new[] {0}, Delay = 0.08f },
                ["S"] = new() { Path = "S", Frames = new[] {0}, Delay = 0.08f },
            },
            Colors = new List<PetColorInfo> {
                new() { Folder = "", Name = "Default" }
            }
        });
    }

    public static List<PetTypeInfo> GetAllPets() {
        if (!loaded) Load();
        return AllPets;
    }

    public static PetTypeInfo GetPet(string id) {
        if (!loaded) Load();
        return AllPets.FirstOrDefault(p => p.Id == id) ?? AllPets[0];
    }

    /// <summary>
    /// Builds a Sprite from code using the atlas, based on the pet's animation definitions.
    /// </summary>
    public static Sprite BuildSprite(PetTypeInfo pet, PetColorInfo color) {
        string basePath = $"objects/MountainPet/{pet.Folder}/";
        if (!string.IsNullOrEmpty(color.Folder))
            basePath += $"{color.Folder}/";

        var sprite = new Sprite(GFX.Game, basePath);

        foreach (var kvp in pet.AnimationDefs) {
            string animId = kvp.Key;
            var def = kvp.Value;

            if (def.IsOneShot) {
                sprite.Add(animId, def.Path, def.Delay, def.Frames);
            } else {
                sprite.AddLoop(animId, def.Path, def.Delay, def.Frames);
            }
        }

        sprite.CenterOrigin();
        sprite.Play("idle");
        return sprite;
    }

    /// <summary>
    /// Resolves the best available animation for a given direction and pet type.
    /// 16 directions fall back to nearest available.
    /// </summary>
    public static string ResolveAnimation(PetTypeInfo pet, CardinalDir dir) {
        string dirName = dir.ToString();

        if (pet.Animations.Contains(dirName))
            return dirName;

        // Fallback: try progressively broader matches
        // Each direction tries its neighbors first, then the nearest cardinal
        string[] fallbacks = dir switch {
            CardinalDir.N => new[] { "NNE", "NNW", "NE", "NW", "idle" },
            CardinalDir.NNE => new[] { "N", "NE", "NEE", "NNW", "idle" },
            CardinalDir.NE => new[] { "NNE", "NEE", "N", "E", "idle" },
            CardinalDir.NEE => new[] { "NE", "E", "NNE", "SEE", "idle" },
            CardinalDir.E => new[] { "NEE", "SEE", "NE", "SE", "idle" },
            CardinalDir.SEE => new[] { "E", "SE", "NEE", "SSE", "idle" },
            CardinalDir.SE => new[] { "SEE", "SSE", "E", "S", "idle" },
            CardinalDir.SSE => new[] { "SE", "S", "SEE", "SSW", "idle" },
            CardinalDir.S => new[] { "SSE", "SSW", "SE", "SW", "idle" },
            CardinalDir.SSW => new[] { "S", "SW", "SSE", "SWW", "idle" },
            CardinalDir.SW => new[] { "SSW", "SWW", "S", "W", "idle" },
            CardinalDir.SWW => new[] { "SW", "W", "SSW", "NWW", "idle" },
            CardinalDir.W => new[] { "SWW", "NWW", "SW", "NW", "idle" },
            CardinalDir.NWW => new[] { "W", "NW", "SWW", "NNW", "idle" },
            CardinalDir.NW => new[] { "NWW", "NNW", "W", "N", "idle" },
            CardinalDir.NNW => new[] { "NW", "N", "NWW", "NNE", "idle" },
            _ => new[] { "idle" }
        };

        // For facing-aware fallback: if we're on the flip side, also try the mirror
        if (pet.Facing == "west") {
            // East-side directions can use their west-side mirror
            string mirror = GetMirrorDirection(dir);
            if (mirror != null && pet.Animations.Contains(mirror))
                return mirror;
        } else if (pet.Facing == "east") {
            // West-side directions can use their east-side mirror
            string mirror = GetMirrorDirection(dir);
            if (mirror != null && pet.Animations.Contains(mirror))
                return mirror;
        }

        foreach (var fallback in fallbacks) {
            if (pet.Animations.Contains(fallback))
                return fallback;
        }

        return "idle";
    }

    /// <summary>
    /// Gets the horizontally mirrored direction name (for FlipX fallback).
    /// </summary>
    private static string GetMirrorDirection(CardinalDir dir) {
        return dir switch {
            CardinalDir.NNE => "NNW",
            CardinalDir.NE => "NW",
            CardinalDir.NEE => "NWW",
            CardinalDir.E => "W",
            CardinalDir.SEE => "SWW",
            CardinalDir.SE => "SW",
            CardinalDir.SSE => "SSW",
            CardinalDir.NNW => "NNE",
            CardinalDir.NW => "NE",
            CardinalDir.NWW => "NEE",
            CardinalDir.W => "E",
            CardinalDir.SWW => "SEE",
            CardinalDir.SW => "SE",
            CardinalDir.SSW => "SSE",
            _ => null // N and S have no mirror
        };
    }

    /// <summary>
    /// Determines if FlipX should be applied for a given direction and facing type.
    /// </summary>
    public static bool ShouldFlip(string facing, CardinalDir dir) {
        return facing switch {
            "west" => dir is CardinalDir.E or CardinalDir.NEE or CardinalDir.NNE
                        or CardinalDir.NE or CardinalDir.SEE or CardinalDir.SE or CardinalDir.SSE,
            "east" => dir is CardinalDir.W or CardinalDir.NWW or CardinalDir.NNW
                        or CardinalDir.NW or CardinalDir.SWW or CardinalDir.SW or CardinalDir.SSW,
            "full" => false,
            _ => false
        };
    }

    private static void RegisterLuma() {
        AllPets.Add(new PetTypeInfo {
            Id = "luma",
            Name = "Luma",
            Folder = "luma",
            Facing = "west",
            HasTransition = false,
            TransitionAnimPath = null,
            Animations = new HashSet<string> { "idle" },
            AnimationDefs = new Dictionary<string, AnimationDef> {
                ["idle"] = new() { Path = "idle", Frames = new[] {0, 1}, Delay = 0.12f },
            },
            Colors = new List<PetColorInfo> {
                new() { Folder = "luma_yellow", Name = "Yellow" },
                new() { Folder = "luma_beige", Name = "Beige" },
                new() { Folder = "luma_black", Name = "Black" },
                new() { Folder = "luma_blue", Name = "Blue" },
                new() { Folder = "luma_pink", Name = "Pink" },
                new() { Folder = "luma_red", Name = "Red" },
                new() { Folder = "luma_white", Name = "White" },
            }
        });
    }
}
