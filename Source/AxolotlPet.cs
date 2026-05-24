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
    N, NE, E, SE, S, SW, W, NW
}

[Tracked]
public class AxolotlPet : Entity {
    private Follower follower;
    private Sprite sprite;

    // Pet type info (resolved at spawn)
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

        // Resolve pet type and color from settings
        petInfo = ResolvePetInfo();
        string spriteBankId = ResolveSpriteBankId();

        try {
            sprite = GFX.SpriteBank.Create(spriteBankId);
        } catch (Exception e) {
            Logger.Log(LogLevel.Error, "MountainPet",
                $"Failed to create sprite '{spriteBankId}': {e.Message}. Falling back.");
            // Fallback to first axolotl color
            try {
                sprite = GFX.SpriteBank.Create("MountainPet_axolotl_tiny_axolotl_pink");
                petInfo = PetRegistry.GetPet("axolotl");
            } catch {
                sprite = new Sprite(GFX.Game, "objects/MountainPet/axolotl/tiny_axolotl_pink/");
                sprite.AddLoop("idle", "idle", 0.5f);
                sprite.CenterOrigin();
                sprite.Play("idle");
                petInfo = PetRegistry.GetPet("axolotl");
            }
        }

        sprite.OnFinish = OnAnimationFinish;
        Add(sprite);
    }

    private static PetTypeInfo ResolvePetInfo() {
        var settings = MountainPetModule.Settings;
        if (settings == null) return PetRegistry.GetPet("axolotl");

        string petId = settings.SelectedPetType switch {
            PetType.Axolotl => "axolotl",
            PetType.Goldfish => "fish",
            _ => "axolotl"
        };
        return PetRegistry.GetPet(petId);
    }

    private static string ResolveSpriteBankId() {
        var settings = MountainPetModule.Settings;
        var pet = ResolvePetInfo();

        // Find the color by folder name from settings
        string colorFolder = settings?.SelectedColorFolder ?? "";
        PetColorInfo color = null;
        foreach (var c in pet.Colors) {
            if (c.Folder == colorFolder) {
                color = c;
                break;
            }
        }
        // Fallback to first color if not found
        color ??= pet.Colors[0];

        return PetRegistry.GetSpriteBankId(pet, color);
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
                    if (petInfo.HasTransition && petInfo.TransitionAnimPath != null) {
                        animState = PetAnimState.TransitionToSwim;
                        sprite.Play(petInfo.TransitionAnimPath);
                        ApplyFlip(currentDir);
                    } else {
                        // No transition animation — go straight to swimming
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
        // Resolve the best available animation for this direction
        string animId = PetRegistry.ResolveAnimation(petInfo, dir);

        if (sprite.CurrentAnimationID != animId)
            sprite.Play(animId);

        ApplyFlip(dir);
    }

    private void ApplyFlip(CardinalDir dir) {
        sprite.FlipX = PetRegistry.ShouldFlip(petInfo.Facing, dir);
    }

    public static CardinalDir ClassifyCardinal(Vector2 velocity) {
        float angle = MathF.Atan2(velocity.X, -velocity.Y) * (180f / MathF.PI);
        if (angle < 0) angle += 360f;

        if (angle >= 337.5f || angle < 22.5f) return CardinalDir.N;
        if (angle >= 22.5f && angle < 67.5f) return CardinalDir.NE;
        if (angle >= 67.5f && angle < 112.5f) return CardinalDir.E;
        if (angle >= 112.5f && angle < 157.5f) return CardinalDir.SE;
        if (angle >= 157.5f && angle < 202.5f) return CardinalDir.S;
        if (angle >= 202.5f && angle < 247.5f) return CardinalDir.SW;
        if (angle >= 247.5f && angle < 292.5f) return CardinalDir.W;
        return CardinalDir.NW;
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
