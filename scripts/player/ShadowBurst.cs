using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// A shadow clone blowing up (Clon señuelo +1/+2, Clon manipulable +3): damages every enemy within
// radius and bursts in the clone's neon colour. Procedural FX (GroundSlamFx chips + a light flash)
// like the rest of the neon kit.
public static class ShadowBurst
{
	public static void Explode(Node context, Vector2 at, float radius, int damage, Color tint)
	{
		foreach (Node node in context.GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node2D enemy || enemy.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 } stats)
				continue;
			Vector2 offset = enemy.GlobalPosition - at;
			if (offset.Length() > radius)
				continue;
			stats.TakeDamage(damage);
			(enemy as Enemy)?.AddShadowMark();
			if (enemy.HasMethod("ApplyKnockback"))
				enemy.Call("ApplyKnockback", offset == Vector2.Zero ? Vector2.Up : offset.Normalized(), 220f);
		}

		var palette = new GroundSlamFx.Palette
		{
			Hot = tint,
			Core = tint.Lightened(0.7f),
			Rock = tint.Darkened(0.6f),
			Dust = tint with { A = 0.4f },
			Light = tint,
		};
		GroundSlamFx.Chip(context, at, Mathf.Clamp(radius / 120f, 0.4f, 1.2f), palette);

		if (context.GetTree().CurrentScene is not Node scene)
			return;
		var flash = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = tint,
			Energy = 2.5f,
			TextureScale = radius / 80f,
		};
		scene.AddChild(flash);
		flash.GlobalPosition = at;
		Tween fade = flash.CreateTween();
		fade.TweenProperty(flash, "energy", 0f, 0.35f);
		fade.TweenCallback(Callable.From(flash.QueueFree));
	}
}
