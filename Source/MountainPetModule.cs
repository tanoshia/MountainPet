using System;
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
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        Everest.Events.Level.OnTransitionTo += OnTransitionTo;
        On.Celeste.Leader.GainFollower += OnLeaderGainFollower;
    }

    public override void Unload() {
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        Everest.Events.Level.OnTransitionTo -= OnTransitionTo;
        On.Celeste.Leader.GainFollower -= OnLeaderGainFollower;
    }

    private void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        // Don't spawn if disabled in settings
        if (!Settings.PetEnabled)
            return;

        // Don't spawn if pet already exists (it persists across rooms)
        if (level.Tracker.GetEntity<AxolotlPet>() != null)
            return;

        // Spawn the pet
        level.Add(new AxolotlPet());
    }

    private void OnTransitionTo(Level level, LevelData next, Vector2 direction) {
        // Reset velocity tracking on room transition to prevent false direction spike
        var pet = level.Tracker.GetEntity<AxolotlPet>();
        pet?.ResetVelocityTracking();
    }

    private static void OnLeaderGainFollower(On.Celeste.Leader.orig_GainFollower orig, Leader self, Follower follower) {
        // Call the original method first
        orig(self, follower);

        // After any follower is added, ensure pet stays at index 0
        if (self.Entity is Player player) {
            var pet = player.Scene?.Tracker.GetEntity<AxolotlPet>();
            if (pet != null) {
                var followers = self.Followers;
                // Find the pet's follower in the list
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
