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
}
