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
    public int NudgeMaxDistance { get; set; } = 24;

    [SettingIgnore]
    public int NudgeMaxOffset { get; set; } = 10;

    [SettingIgnore]
    public int NudgeSpeed { get; set; } = 8;

    // Follow behavior
    [SettingIgnore]
    public int MinMoveDistance { get; set; } = 20;  // min px from player before pet reacts

    // Smooth turning settings
    [SettingIgnore]
    public bool SmoothTurning { get; set; } = true;

    [SettingIgnore]
    public int TurnRadius { get; set; } = 6;  // pixels of arc offset

    [SettingIgnore]
    public int TurnSpeed { get; set; } = 4;   // turn rate multiplier

    // Track whether Advanced section is expanded (not persisted to file, but harmless)
    [SettingIgnore]
    public bool AdvancedExpanded { get; set; } = false;
}
