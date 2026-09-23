using Godot;

namespace Metroidvania.World;

// Homemade melee hit-impact burst for the player's own combo (attack1/attack2/attack3), built to
// replace the bought impact_medium/impact_big sprite sheets for that specific case. Reuses the
// project's existing EnergySpike.gdshader (the radial spike rays already used by the charge aura)
// for the star-burst rays and ForceFieldBubble.gdshader (the ring the parry bubble already uses)
// for the flash ring, instead of writing new shaders — one scene, three tiers.
public partial class HitImpactBurst : Node2D
{
	public enum ImpactTier
	{
		Light,
		Medium,
		Heavy,
	}

	[Export] public ImpactTier Tier = ImpactTier.Medium;

	private Sprite2D _ring;
	private Sprite2D[] _spikes;
	private GpuParticles2D _sparks;
	private PointLight2D _flash;

	public override void _Ready()
	{
		_ring = GetNode<Sprite2D>("Ring");
		_spikes = new[]
		{
			GetNode<Sprite2D>("Spike1"), GetNode<Sprite2D>("Spike2"), GetNode<Sprite2D>("Spike3"),
			GetNode<Sprite2D>("Spike4"), GetNode<Sprite2D>("Spike5"), GetNode<Sprite2D>("Spike6"),
		};
		_sparks = GetNode<GpuParticles2D>("Sparks");
		_flash = GetNode<PointLight2D>("Flash");

		Play();
	}

	private void Play()
	{
		// Only the visible ray count, target scale and spark amount change between tiers — the
		// same shapes just show more/bigger/brighter for the combo finisher than for the jab.
		(float ringScale, int spikeCount, float spikeScale, int sparkAmount, bool flash) = Tier switch
		{
			ImpactTier.Light => (0.5f, 2, 0.25f, 8, false),
			ImpactTier.Heavy => (1.1f, 6, 0.65f, 26, true),
			_ => (0.75f, 4, 0.4f, 16, false),
		};

		_ring.Scale = Vector2.Zero;
		_ring.Modulate = new Color(1f, 1f, 1f, 1f);
		Tween ringTween = CreateTween();
		ringTween.TweenProperty(_ring, "scale", Vector2.One * ringScale, 0.08f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		ringTween.Parallel().TweenProperty(_ring, "modulate:a", 0f, 0.18f).SetDelay(0.04f);

		for (int i = 0; i < _spikes.Length; i++)
		{
			Sprite2D spike = _spikes[i];
			spike.Visible = i < spikeCount;
			if (!spike.Visible)
				continue;

			spike.Scale = new Vector2(spikeScale, 0f);
			spike.Modulate = new Color(1f, 1f, 1f, 1f);
			Tween spikeTween = CreateTween();
			spikeTween.TweenProperty(spike, "scale:y", spikeScale, 0.06f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			spikeTween.Parallel().TweenProperty(spike, "modulate:a", 0f, 0.15f).SetDelay(0.05f);
		}

		_sparks.Amount = sparkAmount;
		_sparks.Restart();
		_sparks.Emitting = true;

		_flash.Energy = 0f;
		if (flash)
		{
			_flash.Energy = 1.6f;
			CreateTween().TweenProperty(_flash, "energy", 0f, 0.18f);
		}

		GetTree().CreateTimer(0.4).Timeout += QueueFree;
	}

	public static void SpawnAt(Node context, Vector2 globalPosition, ImpactTier tier)
	{
		var scene = GD.Load<PackedScene>("res://scenes/world/Rehusables/HitImpactBurst.tscn");
		var burst = scene.Instantiate<HitImpactBurst>();
		burst.Tier = tier;
		context.GetTree().CurrentScene.AddChild(burst);
		burst.GlobalPosition = globalPosition;
	}
}
