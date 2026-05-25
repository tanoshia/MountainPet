using System;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MountainPet;

/// <summary>
/// A Madeline-style death effect scaled down for the pet.
/// 8 copies of a small texture expand outward in a circle, flash between
/// the pet's color and white, then shrink and disappear.
/// Adapted from StrawberryFriend's BerryDeathEffect.
/// </summary>
public class PetDeathEffect : Component {
    public Vector2 Position;
    public Color Color;
    public float Percent;
    public float Duration = 0.7f;
    public Action OnEnd;

    // Scale factor for the effect (pet is ~16px, Madeline is ~32px)
    private const float SizeFactor = 0.6f;

    public PetDeathEffect(Color color, Vector2? offset = null)
        : base(active: true, visible: true) {
        Color = color;
        Position = offset ?? Vector2.Zero;
        Percent = 0f;
    }

    public override void Update() {
        base.Update();

        if (Percent >= 1f) {
            RemoveSelf();
            OnEnd?.Invoke();
            return;
        }

        Percent = Calc.Approach(Percent, 1f, Engine.DeltaTime / Duration);
    }

    public override void Render() {
        if (Entity == null) return;

        Draw(Entity.Position + Position, Color, Percent);
    }

    private void Draw(Vector2 position, Color color, float ease) {
        // Use Madeline's hair node — a small circle, same as vanilla death particles
        MTexture texture = GFX.Game["characters/player/hair00"];

        // Flash between color and white
        Color drawColor = (Math.Floor(ease * 10f) % 2.0 == 0.0) ? color : Color.White;

        // Scale: grow in first half, shrink in second half
        float scale;
        if (ease < 0.5f) {
            scale = (0.5f + ease) * SizeFactor;
        } else {
            scale = Ease.CubeOut(1f - (ease - 0.5f) * 2f) * SizeFactor;
        }

        // Expansion radius — how far the particles spread outward
        float radius = Ease.CubeOut(ease) * 16f * SizeFactor;

        // Draw black outlines (4 offset copies per particle)
        for (int i = 0; i < 8; i++) {
            Vector2 offset = Calc.AngleToVector(
                ((float)i / 8f + ease * 0.25f) * ((float)Math.PI * 2f),
                radius
            );

            texture.DrawCentered(position + offset + new Vector2(-1f, 0f), Color.Black, new Vector2(scale, scale));
            texture.DrawCentered(position + offset + new Vector2(1f, 0f), Color.Black, new Vector2(scale, scale));
            texture.DrawCentered(position + offset + new Vector2(0f, -1f), Color.Black, new Vector2(scale, scale));
            texture.DrawCentered(position + offset + new Vector2(0f, 1f), Color.Black, new Vector2(scale, scale));
        }

        // Draw colored particles on top
        for (int i = 0; i < 8; i++) {
            Vector2 offset = Calc.AngleToVector(
                ((float)i / 8f + ease * 0.25f) * ((float)Math.PI * 2f),
                radius
            );

            texture.DrawCentered(position + offset, drawColor, new Vector2(scale, scale));
        }
    }
}
