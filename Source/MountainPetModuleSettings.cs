using System.Collections.Generic;
using Celeste.Mod;

namespace Celeste.Mod.MountainPet;

public class MountainPetModuleSettings : EverestModuleSettings {
    public bool PetEnabled { get; set; } = true;

    [SettingName("Pet Type")]
    public PetType SelectedPetType { get; set; } = PetType.Axolotl;

    // Stored as the color folder name (e.g., "tiny_axolotl_pink")
    // Displayed in-game using the readable name from PetRegistry
    [SettingIgnore]
    public string SelectedColorFolder { get; set; } = "tiny_axolotl_pink";

    /// <summary>
    /// Creates custom menu entries for the color selector.
    /// Called automatically by Everest for settings with CreateEntry methods.
    /// </summary>
    public void CreateSelectedColorFolderEntry(TextMenu menu, bool inGame) {
        var pet = PetRegistry.GetPetForType(SelectedPetType);
        if (pet == null || pet.Colors.Count == 0) return;

        // Find current index
        int currentIndex = 0;
        for (int i = 0; i < pet.Colors.Count; i++) {
            if (pet.Colors[i].Folder == SelectedColorFolder) {
                currentIndex = i;
                break;
            }
        }

        // Build the display names list
        var colorNames = new List<string>();
        foreach (var color in pet.Colors) {
            colorNames.Add(color.Name);
        }

        // Create a text menu slider that cycles through color names
        var item = new TextMenu.Slider("Pet Color", i => colorNames[i], 0, colorNames.Count - 1, currentIndex);
        item.Change(i => {
            SelectedColorFolder = pet.Colors[i].Folder;
        });
        menu.Add(item);
    }
}

/// <summary>
/// Available pet types. Must match PetRegistry entries.
/// </summary>
public enum PetType {
    Axolotl,
    Goldfish
}
