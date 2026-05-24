using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MountainPet;

public class MountainPetModule : EverestModule {
    public static MountainPetModule Instance { get; private set; }

    public override Type SettingsType => typeof(MountainPetModuleSettings);
    public static MountainPetModuleSettings Settings => (MountainPetModuleSettings) Instance._Settings;

    public MountainPetModule() {
        Instance = this;
#if DEBUG
        Logger.SetLogLevel(nameof(MountainPetModule), LogLevel.Verbose);
#else
        Logger.SetLogLevel(nameof(MountainPetModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        PetRegistry.Load();

        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        Everest.Events.Level.OnTransitionTo += OnTransitionTo;
        On.Celeste.Leader.GainFollower += OnLeaderGainFollower;
    }

    public override void Unload() {
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Everest.Events.Level.OnTransitionTo -= OnTransitionTo;
        On.Celeste.Leader.GainFollower -= OnLeaderGainFollower;
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, FMOD.Studio.EventInstance snapshot) {
        base.CreateModMenuSection(menu, inGame, snapshot);

        var pets = PetRegistry.GetAllPets();
        if (pets.Count == 0) return;

        // Pet Type slider
        int currentPetIndex = 0;
        for (int i = 0; i < pets.Count; i++) {
            if (pets[i].Id == Settings.SelectedPetId) {
                currentPetIndex = i;
                break;
            }
        }

        var petNames = new List<string>();
        foreach (var p in pets) petNames.Add(p.Name);

        // Create one color slider and one randomize toggle per pet type (only active ones visible)
        var colorSliders = new List<TextMenu.Slider>();
        var randomizeToggles = new List<TextMenu.OnOff>();
        foreach (var pet in pets) {
            if (pet.Colors.Count <= 1) {
                colorSliders.Add(null);
                randomizeToggles.Add(null);
                continue;
            }

            int colorIndex = 0;
            if (pet.Id == Settings.SelectedPetId) {
                for (int i = 0; i < pet.Colors.Count; i++) {
                    if (pet.Colors[i].Folder == Settings.SelectedColorFolder) {
                        colorIndex = i;
                        break;
                    }
                }
            }

            var names = new List<string>();
            foreach (var c in pet.Colors) names.Add(c.Name);

            var slider = new TextMenu.Slider($"{pet.Name} Color", i => names[i], 0, names.Count - 1, colorIndex);
            var capturedPet = pet; // Capture for closure
            slider.Change(i => {
                Settings.SelectedColorFolder = capturedPet.Colors[i].Folder;
            });

            bool isSelected = (pet.Id == Settings.SelectedPetId);
            // Color slider hidden if randomize is on or pet not selected
            slider.Visible = isSelected && !Settings.RandomizeColor;
            colorSliders.Add(slider);

            // Randomize toggle
            var randomToggle = new TextMenu.OnOff("Randomize Color", Settings.RandomizeColor);
            randomToggle.Visible = isSelected;
            var capturedSlider = slider;
            randomToggle.Change(val => {
                Settings.RandomizeColor = val;
                // Hide color slider when randomize is on
                capturedSlider.Visible = !val;
            });
            randomizeToggles.Add(randomToggle);
        }

        // Pet type slider — toggles color slider and randomize toggle visibility
        menu.Add(new TextMenu.Slider("Pet Type", i => petNames[i], 0, petNames.Count - 1, currentPetIndex)
            .Change(i => {
                Settings.SelectedPetId = pets[i].Id;
                if (pets[i].Colors.Count > 0)
                    Settings.SelectedColorFolder = pets[i].Colors[0].Folder;

                // Show/hide color sliders and randomize toggles
                for (int j = 0; j < colorSliders.Count; j++) {
                    if (randomizeToggles[j] != null) {
                        randomizeToggles[j].Visible = (j == i);
                    }
                    if (colorSliders[j] != null) {
                        colorSliders[j].Visible = (j == i) && !Settings.RandomizeColor;
                        if (j == i) colorSliders[j].Index = 0;
                    }
                }
            }));

        // Add all randomize toggles and color sliders (only the active ones are visible)
        for (int i = 0; i < pets.Count; i++) {
            if (randomizeToggles[i] != null)
                menu.Add(randomizeToggles[i]);
            if (colorSliders[i] != null)
                menu.Add(colorSliders[i]);
        }

        // Pet Light toggle
        menu.Add(new TextMenu.OnOff("Pet Light", Settings.PetLight)
            .Change(val => {
                Settings.PetLight = val;
            }));

        // Min Follow Distance — top-level setting (not in Advanced)
        menu.Add(new TextMenu.Slider("Min Follow Distance (default 28)",
            i => $"{i}px", 12, 48, Settings.MinMoveDistance)
            .Change(val => { Settings.MinMoveDistance = val; }));

        // === Advanced section (clickable expand/collapse) ===
        // All advanced sub-items
        var advancedItems = new List<TextMenu.Item>();

        // -- Nudge settings --
        var nudgeToggle = new TextMenu.OnOff("  Nudge Away From Player", Settings.NudgeEnabled);
        advancedItems.Add(nudgeToggle);

        var nudgeDistSlider = new TextMenu.Slider("    Trigger Radius",
            i => $"{i}px", 4, 64, Settings.NudgeMaxDistance);
        nudgeDistSlider.Change(val => { Settings.NudgeMaxDistance = val; });
        nudgeDistSlider.Visible = Settings.NudgeEnabled;
        advancedItems.Add(nudgeDistSlider);

        var nudgeOffsetSlider = new TextMenu.Slider("    Offset",
            i => $"{i}px", 2, 32, Settings.NudgeMaxOffset);
        nudgeOffsetSlider.Change(val => { Settings.NudgeMaxOffset = val; });
        nudgeOffsetSlider.Visible = Settings.NudgeEnabled;
        advancedItems.Add(nudgeOffsetSlider);

        var nudgeSpeedSlider = new TextMenu.Slider("    Speed",
            i => $"{i}", 1, 16, Settings.NudgeSpeed);
        nudgeSpeedSlider.Change(val => { Settings.NudgeSpeed = val; });
        nudgeSpeedSlider.Visible = Settings.NudgeEnabled;
        advancedItems.Add(nudgeSpeedSlider);

        nudgeToggle.Change(val => {
            Settings.NudgeEnabled = val;
            nudgeDistSlider.Visible = val && Settings.AdvancedExpanded;
            nudgeOffsetSlider.Visible = val && Settings.AdvancedExpanded;
            nudgeSpeedSlider.Visible = val && Settings.AdvancedExpanded;
        });

        // -- Smooth turning settings --
        var turnToggle = new TextMenu.OnOff("  Smooth Turning", Settings.SmoothTurning);
        advancedItems.Add(turnToggle);

        var turnRadiusSlider = new TextMenu.Slider("    Turn Radius",
            i => $"{i}px", 2, 64, Settings.TurnRadius);
        turnRadiusSlider.Change(val => { Settings.TurnRadius = val; });
        turnRadiusSlider.Visible = Settings.SmoothTurning;
        advancedItems.Add(turnRadiusSlider);

        var turnSpeedSlider = new TextMenu.Slider("    Turn Speed",
            i => $"{i}", 1, 20, Settings.TurnSpeed);
        turnSpeedSlider.Change(val => { Settings.TurnSpeed = val; });
        turnSpeedSlider.Visible = Settings.SmoothTurning;
        advancedItems.Add(turnSpeedSlider);

        turnToggle.Change(val => {
            Settings.SmoothTurning = val;
            turnRadiusSlider.Visible = val && Settings.AdvancedExpanded;
            turnSpeedSlider.Visible = val && Settings.AdvancedExpanded;
        });

        // Set initial visibility based on expanded state
        foreach (var item in advancedItems) {
            // Sub-sliders that depend on their parent toggle keep their own logic;
            // but all items are hidden if Advanced is collapsed
            if (!Settings.AdvancedExpanded)
                item.Visible = false;
        }

        // The clickable "Advanced" button
        var advancedButton = new TextMenu.Button(
            Settings.AdvancedExpanded ? "Advanced \u25b2" : "Advanced \u25bc");
        advancedButton.Pressed(() => {
            Settings.AdvancedExpanded = !Settings.AdvancedExpanded;
            advancedButton.Label = Settings.AdvancedExpanded ? "Advanced \u25b2" : "Advanced \u25bc";

            if (Settings.AdvancedExpanded) {
                // Show all top-level advanced items
                nudgeToggle.Visible = true;
                nudgeDistSlider.Visible = Settings.NudgeEnabled;
                nudgeOffsetSlider.Visible = Settings.NudgeEnabled;
                nudgeSpeedSlider.Visible = Settings.NudgeEnabled;
                turnToggle.Visible = true;
                turnRadiusSlider.Visible = Settings.SmoothTurning;
                turnSpeedSlider.Visible = Settings.SmoothTurning;
            } else {
                // Hide everything
                foreach (var item in advancedItems)
                    item.Visible = false;
            }
        });
        menu.Add(advancedButton);

        // Add all advanced items to menu
        foreach (var item in advancedItems)
            menu.Add(item);
    }

    private void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        if (!Settings.PetEnabled)
            return;

        if (level.Tracker.GetEntity<AxolotlPet>() != null)
            return;

        level.Add(new AxolotlPet());
    }

    private void OnTransitionTo(Level level, LevelData next, Vector2 direction) {
        var pet = level.Tracker.GetEntity<AxolotlPet>();
        pet?.ResetVelocityTracking();
    }

    private static void OnLeaderGainFollower(On.Celeste.Leader.orig_GainFollower orig, Leader self, Follower follower) {
        orig(self, follower);

        if (self.Entity is Player player) {
            var pet = player.Scene?.Tracker.GetEntity<AxolotlPet>();
            if (pet != null) {
                var followers = self.Followers;
                for (int i = 1; i < followers.Count; i++) {
                    if (followers[i].Entity is AxolotlPet) {
                        var petFollower = followers[i];
                        followers.RemoveAt(i);
                        followers.Insert(0, petFollower);
                        break;
                    }
                }
            }
        }
    }
}
