using System;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MountainPet;

public enum PetAnimState {
    Idle,
    TransitionToSwim,
    Swimming,
    Rest
}

public enum CardinalDir {
    N, NNE, NE, NEE, E, SEE, SE, SSE, S, SSW, SW, SWW, W, NWW, NW, NNW
}

[Tracked]
public class AxolotlPet : Entity {
    private Follower follower;
    private Sprite sprite;

    // Pet type info (resolved at spawn from settings + registry)
    private PetTypeInfo petInfo;

    // Animation state
    private Vector2 lastPosition;
    private PetAnimState animState = PetAnimState.Idle;
    private CardinalDir currentDir = CardinalDir.W;
    private const float MoveThreshold = 0.3f;

    public AxolotlPet()
        : base(Vector2.Zero) {
        Depth = 1;
        Tag = Tags.Persistent | Tags.TransitionUpdate;

        follower = new Follower();
        Add(follower);

        // Resolve pet type and color from settings + registry
        petInfo = ResolvePetInfo();
        var color = ResolveColor(petInfo);

        try {
            sprite = PetRegistry.BuildSprite(petInfo, color);
        } catch (Exception e) {
            Logger.Log(LogLevel.Error, "MountainPet",
                $"Failed to build sprite for {petInfo.Id}/{color.Folder}: {e.Message}");
            // Fallback: minimal sprite
            sprite = new Sprite(GFX.Game, "objects/MountainPet/axolotl/tiny_axolotl_pink/");
            sprite.AddLoop("idle", "idle", 0.5f);
            sprite.CenterOrigin();
            sprite.Play("idle");
            petInfo = PetRegistry.GetPet("axolotl");
        }

        sprite.OnFinish = OnAnimationFinish;
        Add(sprite);

        // Add light if setting is enabled
        if (MountainPetModule.Settings?.PetLight == true) {
            Add(new VertexLight(Color.White, 1f, 32, 64));
        }
    }

    private static PetTypeInfo ResolvePetInfo() {
        var settings = MountainPetModule.Settings;
        string petId = settings?.SelectedPetId ?? "axolotl";
        return PetRegistry.GetPet(petId);
    }

    private static PetColorInfo ResolveColor(PetTypeInfo pet) {
        var settings = MountainPetModule.Settings;

        // If randomize is enabled and the pet has multiple colors, pick a different one
        if (settings?.RandomizeColor == true && pet.Colors.Count > 1) {
            string lastFolder = settings.SelectedColorFolder ?? "";
            int index;
            do {
                index = Calc.Random.Next(pet.Colors.Count);
            } while (pet.Colors[index].Folder == lastFolder);

            // Store the chosen color so next spawn avoids it
            settings.SelectedColorFolder = pet.Colors[index].Folder;
            return pet.Colors[index];
        }

        string colorFolder = settings?.SelectedColorFolder ?? "";

        foreach (var c in pet.Colors) {
            if (c.Folder == colorFolder)
                return c;
        }
        return pet.Colors[0];
    }

    public override void Added(Scene scene) {
        base.Added(scene);
        AttachToPlayer();
        lastPosition = Position;
    }

    public override void Update() {
        base.Update();

        if (follower.Leader == null) {
            AttachToPlayer();
        }

        Vector2 velocity = Position - lastPosition;
        float speed = velocity.Length();
        bool isMoving = speed >= MoveThreshold;

        switch (animState) {
            case PetAnimState.Idle:
            case PetAnimState.Rest:
                if (isMoving) {
                    currentDir = ClassifyCardinal(velocity);
                    if (petInfo.HasTransition && petInfo.TransitionAnimPath != null
                        && sprite.Animations.ContainsKey(petInfo.TransitionAnimPath)) {
                        animState = PetAnimState.TransitionToSwim;
                        sprite.Play(petInfo.TransitionAnimPath);
                        ApplyFlip(currentDir);
                    } else {
                        animState = PetAnimState.Swimming;
                        PlaySwimAnim(currentDir);
                    }
                }
                break;

            case PetAnimState.TransitionToSwim:
                if (!isMoving) {
                    animState = PetAnimState.Idle;
                    sprite.Play("idle");
                } else {
                    currentDir = ClassifyCardinal(velocity);
                    ApplyFlip(currentDir);
                }
                break;

            case PetAnimState.Swimming:
                if (!isMoving) {
                    animState = PetAnimState.Idle;
                    sprite.Play("idle");
                } else {
                    CardinalDir newDir = ClassifyCardinal(velocity);
                    if (newDir != currentDir) {
                        currentDir = newDir;
                        PlaySwimAnim(currentDir);
                    }
                }
                break;
        }

        lastPosition = Position;
    }

    private void OnAnimationFinish(string animId) {
        if (petInfo.HasTransition && animId == petInfo.TransitionAnimPath) {
            animState = PetAnimState.Swimming;
            PlaySwimAnim(currentDir);
        }
    }

    private void PlaySwimAnim(CardinalDir dir) {
        string animId = PetRegistry.ResolveAnimation(petInfo, dir);

        // Safety: only play if the animation was actually registered on this sprite
        if (sprite.Animations.ContainsKey(animId)) {
            if (sprite.CurrentAnimationID != animId)
                sprite.Play(animId);
        }

        ApplyFlip(dir);
    }

    private void ApplyFlip(CardinalDir dir) {
        sprite.FlipX = PetRegistry.ShouldFlip(petInfo.Facing, dir);
    }

    public static CardinalDir ClassifyCardinal(Vector2 velocity) {
        // Clock-style: 0°=N, 90°=E, 180°=S, 270°=W
        float angle = MathF.Atan2(velocity.X, -velocity.Y) * (180f / MathF.PI);
        if (angle < 0) angle += 360f;

        // 16 sectors of 22.5° each
        if (angle >= 348.75f || angle < 11.25f) return CardinalDir.N;
        if (angle >= 11.25f && angle < 33.75f) return CardinalDir.NNE;
        if (angle >= 33.75f && angle < 56.25f) return CardinalDir.NE;
        if (angle >= 56.25f && angle < 78.75f) return CardinalDir.NEE;
        if (angle >= 78.75f && angle < 101.25f) return CardinalDir.E;
        if (angle >= 101.25f && angle < 123.75f) return CardinalDir.SEE;
        if (angle >= 123.75f && angle < 146.25f) return CardinalDir.SE;
        if (angle >= 146.25f && angle < 168.75f) return CardinalDir.SSE;
        if (angle >= 168.75f && angle < 191.25f) return CardinalDir.S;
        if (angle >= 191.25f && angle < 213.75f) return CardinalDir.SSW;
        if (angle >= 213.75f && angle < 236.25f) return CardinalDir.SW;
        if (angle >= 236.25f && angle < 258.75f) return CardinalDir.SWW;
        if (angle >= 258.75f && angle < 281.25f) return CardinalDir.W;
        if (angle >= 281.25f && angle < 303.75f) return CardinalDir.NWW;
        if (angle >= 303.75f && angle < 326.25f) return CardinalDir.NW;
        return CardinalDir.NNW; // 326.25 - 348.75
    }

    public void ResetVelocityTracking() {
        lastPosition = Position;
    }

    private void AttachToPlayer() {
        Player player = Scene?.Tracker.GetEntity<Player>();
        if (player != null && follower.Leader == null) {
            Position = player.Position;
            lastPosition = Position;
            player.Leader.GainFollower(follower);
            EnsureFirstInChain(player);
        }
    }

    private void EnsureFirstInChain(Player player) {
        var followers = player.Leader.Followers;
        if (followers.Count > 1 && followers[0] != follower) {
            followers.Remove(follower);
            followers.Insert(0, follower);
        }
    }
}
