namespace Celeste.Mod.MountainPet;

public class MountainPetModuleSettings : EverestModuleSettings {
    public bool PetEnabled { get; set; } = true;

    [SettingIgnore]
    public string SelectedPetId { get; set; } = "axolotl";

    [SettingIgnore]
    public string SelectedColorFolder { get; set; } = "tiny_axolotl_pink";

    [SettingIgnore]
    public bool RandomizeColor { get; set; } = false;

    [SettingIgnore]
    public bool PetLight { get; set; } = false;

    // Proximity nudge settings
    [SettingIgnore]
    public bool NudgeEnabled { get; set; } = true;

    [SettingIgnore]
    public int NudgeMaxDistance { get; set; } = 24;  // trigger radius in pixels

    [SettingIgnore]
    public int NudgeMaxOffset { get; set; } = 10;   // max push in pixels

    [SettingIgnore]
    public int NudgeSpeed { get; set; } = 8;        // lerp speed multiplier
}
