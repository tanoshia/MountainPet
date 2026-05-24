using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.MountainPet;

/// <summary>
/// Defines a pet type with its available animations, colors, and art facing direction.
/// </summary>
public class PetTypeInfo {
    public string Id { get; set; }
    public string Name { get; set; }
    public string Folder { get; set; }
    public string Facing { get; set; } // "west", "east", or "full"
    public HashSet<string> Animations { get; set; } // Available animation IDs (N, S, W, NW, SW, E, NE, SE, idle, rest, idle_to_swim)
    public List<PetColorInfo> Colors { get; set; }
    public bool HasTransition { get; set; } // Whether idle_to_swim exists
    public string TransitionAnimPath { get; set; } // Override path for transition anim (e.g., "idle_to_W")
}

public class PetColorInfo {
    public string Folder { get; set; }
    public string Name { get; set; }
}

/// <summary>
/// Registry of all available pet types. Hardcoded for now (can be loaded from JSON later).
/// </summary>
public static class PetRegistry {
    private static readonly List<PetTypeInfo> AllPets = new();

    static PetRegistry() {
        // Axolotl — west-facing art, full directional set
        AllPets.Add(new PetTypeInfo {
            Id = "axolotl",
            Name = "Axolotl",
            Folder = "axolotl",
            Facing = "west",
            Animations = new HashSet<string> { "idle", "rest", "N", "S", "W", "NW", "SW" },
            HasTransition = true,
            TransitionAnimPath = "idle_to_W",
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

        // Goldfish — east-facing art, limited directional set
        AllPets.Add(new PetTypeInfo {
            Id = "fish",
            Name = "Goldfish",
            Folder = "fish",
            Facing = "east",
            Animations = new HashSet<string> { "idle", "E", "NE", "SE" },
            HasTransition = false,
            TransitionAnimPath = null,
            Colors = new List<PetColorInfo> {
                new() { Folder = "", Name = "Default" }
            }
        });
    }

    public static List<PetTypeInfo> GetAllPets() => AllPets;

    public static PetTypeInfo GetPet(string id) =>
        AllPets.FirstOrDefault(p => p.Id == id) ?? AllPets[0];

    public static PetTypeInfo GetPetForType(PetType type) {
        string id = type switch {
            PetType.Axolotl => "axolotl",
            PetType.Goldfish => "fish",
            _ => "axolotl"
        };
        return GetPet(id);
    }

    public static PetTypeInfo GetPetByIndex(int index) =>
        index >= 0 && index < AllPets.Count ? AllPets[index] : AllPets[0];

    public static int GetPetIndex(string id) {
        for (int i = 0; i < AllPets.Count; i++) {
            if (AllPets[i].Id == id) return i;
        }
        return 0;
    }

    /// <summary>
    /// Gets the SpriteBank ID for a given pet type and color.
    /// Format: MountainPet_{petFolder}_{colorFolder} or MountainPet_{petFolder} if no color subfolder.
    /// </summary>
    public static string GetSpriteBankId(PetTypeInfo pet, PetColorInfo color) {
        if (string.IsNullOrEmpty(color.Folder))
            return $"MountainPet_{pet.Folder}";
        return $"MountainPet_{pet.Folder}_{color.Folder}";
    }

    /// <summary>
    /// Resolves the best available animation for a given direction and pet type.
    /// Falls back to closest available direction, ultimately to idle.
    /// </summary>
    public static string ResolveAnimation(PetTypeInfo pet, CardinalDir dir) {
        string dirName = dir.ToString();

        // Direct match
        if (pet.Animations.Contains(dirName))
            return dirName;

        // Fallback chains per direction
        string[] fallbacks = dir switch {
            CardinalDir.N => new[] { "NW", "NE", "W", "E", "idle" },
            CardinalDir.NE => new[] { "E", "N", "NW", "SE", "idle" },
            CardinalDir.E => new[] { "NE", "SE", "W", "idle" },
            CardinalDir.SE => new[] { "E", "S", "NE", "SW", "idle" },
            CardinalDir.S => new[] { "SW", "SE", "W", "E", "idle" },
            CardinalDir.SW => new[] { "W", "S", "NW", "SE", "idle" },
            CardinalDir.W => new[] { "NW", "SW", "E", "idle" },
            CardinalDir.NW => new[] { "W", "N", "SW", "NE", "idle" },
            _ => new[] { "idle" }
        };

        foreach (var fallback in fallbacks) {
            if (pet.Animations.Contains(fallback))
                return fallback;
        }

        return "idle";
    }

    /// <summary>
    /// Determines if FlipX should be applied for a given direction and facing type.
    /// </summary>
    public static bool ShouldFlip(string facing, CardinalDir dir) {
        return facing switch {
            "west" => dir is CardinalDir.E or CardinalDir.NE or CardinalDir.SE,
            "east" => dir is CardinalDir.W or CardinalDir.NW or CardinalDir.SW,
            "full" => false,
            _ => false
        };
    }
}
