using System;
using System.Collections;
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
    private bool isDying = false;

    // Animation state
    private Vector2 lastPosition;
    private PetAnimState animState = PetAnimState.Idle;
    private CardinalDir currentDir = CardinalDir.W;
    private const float BaseMoveThreshold = 0.3f;

    // Position freeze: hold pet in place until player exceeds min follow distance
    private bool isHeld = false;
    private Vector2 heldPosition;

    // Lateral nudge: pushes sprite to the side when pet is directly above/below player
    private float lateralNudge = 0f;
    private float verticalNudge = 0f;

    // Smooth turning: arc offset when direction changes sharply
    private float displayAngle = 270f; // Current visual angle (degrees, 0=N, 90=E, 180=S, 270=W)
    private float arcOffsetX = 0f;
    private float arcOffsetY = 0f;
    private const float ArcDecayRate = 6f; // How fast arc offset decays back to zero

    // Read from settings (with fallback defaults)
    private float NudgeMaxDistance => MountainPetModule.Settings?.NudgeMaxDistance ?? 24f;
    private float NudgeMaxOffset => MountainPetModule.Settings?.NudgeMaxOffset ?? 10f;
    private float NudgeLerpSpeed => MountainPetModule.Settings?.NudgeSpeed ?? 8f;
    private float TurnRadius => MountainPetModule.Settings?.TurnRadius ?? 6f;
    private float TurnSpeed => MountainPetModule.Settings?.TurnSpeed ?? 4f;

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

        // Skip all movement/animation logic while dying
        if (isDying) return;

        if (follower.Leader == null) {
            AttachToPlayer();
        }

        // Min move distance: pet freezes in place until player is at least this far away
        // Value of 0 means disabled (use game default follower behavior)
        // Also disabled during cutscenes so the pet follows naturally
        float minDist = MountainPetModule.Settings?.MinMoveDistance ?? 20;
        bool inCutscene = (Scene as Level)?.InCutscene == true;
        Player playerForDist = Scene?.Tracker.GetEntity<Player>();
        float distToPlayer = playerForDist != null ? (playerForDist.Position - Position).Length() : float.MaxValue;

        // Position freeze logic (disabled when minDist is 0 or during cutscenes)
        if (minDist > 0 && !inCutscene && distToPlayer < minDist) {
            if (!isHeld) {
                // Start holding — remember where we are
                isHeld = true;
                heldPosition = Position;
            }
            // Snap back to held position (override follower system)
            Position = heldPosition;
        } else {
            // Player is far enough — release the hold
            isHeld = false;
        }

        // Recalculate distance after potential position override
        if (playerForDist != null)
            distToPlayer = (playerForDist.Position - Position).Length();

        Vector2 velocity = Position - lastPosition;
        float speed = velocity.Length();
        bool isMoving = speed >= BaseMoveThreshold && !isHeld;

        if (isMoving) {
            float targetAngle = VelocityToAngle(velocity);
            CardinalDir displayDir;

            if (MountainPetModule.Settings?.SmoothTurning == true) {
                // Smooth turning: rotate displayAngle toward target with max turn rate
                float angleDiff = AngleDifference(displayAngle, targetAngle);

                // Turn rate scales with:
                // 1. TurnSpeed setting (base rate)
                // 2. Pet's current speed (faster movement = faster turning)
                // 3. Distance to player (further away = faster turning to catch up)
                float speedFactor = MathHelper.Clamp(speed / 3f, 0.5f, 3f); // normalize around typical speed
                float turnDistFactor = MathHelper.Clamp(distToPlayer / 40f, 0.5f, 3f); // normalize: 40px = 1x
                float maxTurnPerFrame = 180f * TurnSpeed * speedFactor * turnDistFactor * Engine.DeltaTime;

                if (MathF.Abs(angleDiff) <= maxTurnPerFrame) {
                    displayAngle = targetAngle;
                } else {
                    displayAngle += MathF.Sign(angleDiff) * maxTurnPerFrame;
                    displayAngle = NormalizeAngle(displayAngle);
                }

                // Arc offset: push perpendicular to movement during turns
                // Offset goes opposite to turn direction so the HEAD stays on path
                // and the TAIL swings outward (like a fish turning)
                float turnStrength = MathHelper.Clamp(MathF.Abs(angleDiff) / 90f, 0f, 1f);
                float perpAngleRad = (displayAngle - 90f * MathF.Sign(angleDiff)) * (MathF.PI / 180f);
                float arcTarget = turnStrength * TurnRadius;
                // Perpendicular direction (90° to display angle, in the turn direction)
                float perpX = MathF.Sin(perpAngleRad) * arcTarget;
                float perpY = -MathF.Cos(perpAngleRad) * arcTarget;

                arcOffsetX = MathHelper.Lerp(arcOffsetX, perpX, ArcDecayRate * Engine.DeltaTime);
                arcOffsetY = MathHelper.Lerp(arcOffsetY, perpY, ArcDecayRate * Engine.DeltaTime);

                displayDir = AngleToCardinal(displayAngle);
            } else {
                // Instant turning (original behavior)
                displayAngle = targetAngle;
                displayDir = ClassifyCardinal(velocity);
                arcOffsetX = 0f;
                arcOffsetY = 0f;
            }

            switch (animState) {
                case PetAnimState.Idle:
                case PetAnimState.Rest:
                    currentDir = displayDir;
                    if (petInfo.HasTransition && petInfo.TransitionAnimPath != null
                        && sprite.Animations.ContainsKey(petInfo.TransitionAnimPath)) {
                        animState = PetAnimState.TransitionToSwim;
                        sprite.Play(petInfo.TransitionAnimPath);
                        ApplyFlip(currentDir);
                    } else {
                        animState = PetAnimState.Swimming;
                        PlaySwimAnim(currentDir);
                    }
                    break;

                case PetAnimState.TransitionToSwim:
                    currentDir = displayDir;
                    ApplyFlip(currentDir);
                    break;

                case PetAnimState.Swimming:
                    if (displayDir != currentDir) {
                        currentDir = displayDir;
                        PlaySwimAnim(currentDir);
                    }
                    break;
            }
        } else {
            // Not moving (or held in place)
            if (animState != PetAnimState.Idle) {
                animState = PetAnimState.Idle;
                sprite.Play("idle");
            }
            // Still flip the idle sprite to face toward the player
            // and sync displayAngle so smooth turning starts from the correct direction
            if (playerForDist != null) {
                Vector2 toPlayer = playerForDist.Position - Position;
                if (MathF.Abs(toPlayer.X) > 1f) {
                    // Face toward the player
                    CardinalDir facingDir = ClassifyCardinal(toPlayer);
                    ApplyFlip(facingDir);
                    // Sync displayAngle so movement starts from this direction
                    displayAngle = VelocityToAngle(toPlayer);
                }
            }
            // Decay arc offset when stopped
            arcOffsetX = Calc.Approach(arcOffsetX, 0f, ArcDecayRate * Engine.DeltaTime * TurnRadius);
            arcOffsetY = Calc.Approach(arcOffsetY, 0f, ArcDecayRate * Engine.DeltaTime * TurnRadius);
        }

        lastPosition = Position;

        // Proximity nudge: push sprite away from player when too close
        ApplyProximityNudge();

        // Combine arc offset with nudge offset
        sprite.X = lateralNudge + arcOffsetX;
        sprite.Y = verticalNudge + arcOffsetY;
    }

    private void ApplyProximityNudge() {
        // Check if nudge is disabled in settings
        if (MountainPetModule.Settings?.NudgeEnabled != true) {
            lateralNudge = 0f;
            verticalNudge = 0f;
            return;
        }

        Player player = Scene?.Tracker.GetEntity<Player>();
        float dt = Engine.DeltaTime;

        if (player == null) {
            lateralNudge = Calc.Approach(lateralNudge, 0f, NudgeLerpSpeed * dt);
            verticalNudge = Calc.Approach(verticalNudge, 0f, NudgeLerpSpeed * dt);
            return;
        }

        Vector2 toPlayer = player.Position - Position;
        float dist = toPlayer.Length();

        float maxDist = NudgeMaxDistance;
        float maxOffset = NudgeMaxOffset;
        float speed = NudgeLerpSpeed;

        if (dist > 0.1f && dist < maxDist) {
            float distFactor = 1f - (dist / maxDist);

            // Compute the "away from player" angle
            // toPlayer points toward player, so we want the opposite direction
            float awayAngle = MathF.Atan2(-toPlayer.X, toPlayer.Y) * (180f / MathF.PI);
            // Normalize to 0-360 (0=N, 90=E, 180=S, 270=W)
            if (awayAngle < 0f) awayAngle += 360f;

            // Clamp the away angle to avoid dead zones:
            // Dead zone: ±30° of North (330-360, 0-30) and ±45° of South (135-225)
            awayAngle = ClampNudgeAngle(awayAngle);

            // Convert clamped angle to X/Y offset
            float angleRad = awayAngle * (MathF.PI / 180f);
            float targetX = MathF.Sin(angleRad) * maxOffset * distFactor;
            float targetY = -MathF.Cos(angleRad) * maxOffset * distFactor;

            // How close to the player's vertical axis? Stronger nudge when more aligned
            float absX = MathF.Abs(toPlayer.X);
            float absY = MathF.Abs(toPlayer.Y);
            float alignFactor = 1f - MathHelper.Clamp(absX / (maxDist * 0.5f), 0f, 1f);

            targetX *= alignFactor;
            targetY *= alignFactor;

            lateralNudge = MathHelper.Lerp(lateralNudge, targetX, speed * dt);
            verticalNudge = MathHelper.Lerp(verticalNudge, targetY, speed * dt);
        } else {
            lateralNudge = Calc.Approach(lateralNudge, 0f, speed * dt * 2f);
            verticalNudge = Calc.Approach(verticalNudge, 0f, speed * dt * 2f);
        }
    }

    /// <summary>
    /// Clamps a nudge angle away from dead zones (above and below player).
    /// Dead zones: ±30° of North (0°), ±45° of South (180°).
    /// Pushes toward the nearest side (East=90° or West=270°).
    /// </summary>
    private static float ClampNudgeAngle(float angle) {
        // North dead zone: 330-360 and 0-30 → push to nearest side
        if (angle < 30f) {
            // 0-30: push toward East (90)
            return 30f + (90f - 30f) * (angle / 30f); // remap 0-30 → 30-90 (bias toward 90)
        }
        if (angle > 330f) {
            // 330-360: push toward West (270)
            return 330f - (330f - 270f) * ((360f - angle) / 30f); // remap 330-360 → 270-330 (bias toward 270)
        }

        // South dead zone: 135-225 → push to nearest side
        if (angle >= 135f && angle <= 180f) {
            // 135-180: push toward East (90)
            return 135f - (135f - 90f) * ((angle - 135f) / 45f); // remap 135-180 → 135-90 (bias toward 90)
        }
        if (angle > 180f && angle <= 225f) {
            // 180-225: push toward West (270)
            return 225f + (270f - 225f) * ((angle - 180f) / 45f); // remap 180-225 → 225-270 (bias toward 270)
        }

        // Outside dead zones — angle is fine
        return angle;
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
        float angle = VelocityToAngle(velocity);
        return AngleToCardinal(angle);
    }

    /// <summary>
    /// Converts a velocity vector to an angle in degrees (0=N, 90=E, 180=S, 270=W).
    /// </summary>
    private static float VelocityToAngle(Vector2 velocity) {
        float angle = MathF.Atan2(velocity.X, -velocity.Y) * (180f / MathF.PI);
        if (angle < 0) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Converts an angle (0=N, 90=E, 180=S, 270=W) to a CardinalDir.
    /// </summary>
    private static CardinalDir AngleToCardinal(float angle) {
        angle = NormalizeAngle(angle);
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
        return CardinalDir.NNW;
    }

    /// <summary>
    /// Shortest signed angle difference from 'from' to 'to' (result in -180..180).
    /// </summary>
    private static float AngleDifference(float from, float to) {
        float diff = NormalizeAngle(to) - NormalizeAngle(from);
        if (diff > 180f) diff -= 360f;
        if (diff < -180f) diff += 360f;
        return diff;
    }

    /// <summary>
    /// Normalizes angle to 0..360 range.
    /// </summary>
    private static float NormalizeAngle(float angle) {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
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

    /// <summary>
    /// Triggers the pet death animation (called when the player dies).
    /// </summary>
    public void Kill() {
        if (isDying) return;
        isDying = true;
        Add(new Coroutine(DeathAnimation()));
    }

    private IEnumerator DeathAnimation() {
        // Detach from follower chain so the pet stays in place
        if (follower.Leader != null) {
            follower.Leader.LoseFollower(follower);
        }

        // Brief pause before the burst (pet "reacts" to death)
        yield return 0.05f;

        // Hide the sprite
        sprite.Visible = false;

        // Add the expanding death burst effect
        var deathColor = GetPetColor();
        Add(new PetDeathEffect(deathColor, Center - Position) {
            OnEnd = () => RemoveSelf()
        });

        // Displacement burst (subtle screen warp)
        SceneAs<Level>()?.Displacement.AddBurst(Center, 0.2f, 4f, 32f, 0.4f);
    }

    /// <summary>
    /// Returns a representative color for the current pet (used for death effect tinting).
    /// </summary>
    private Color GetPetColor() {
        // Use a color based on the pet type for a nice tint
        return petInfo.Id switch {
            "axolotl" => new Color(255, 150, 180),  // Soft pink
            "fish" => new Color(255, 180, 80),       // Gold
            "luma" => new Color(255, 255, 150),      // Warm yellow
            _ => Color.White
        };
    }
}
