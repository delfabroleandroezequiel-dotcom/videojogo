using Godot;

namespace Metroidvania.World;

// Standalone preview for the three "casero" VFX (flamethrower, lightning, poison spit) — run
// this scene directly (F6 in Godot) to see all three looping without wiring them into any enemy
// or ability first.
public partial class VfxLabDemo : Node2D
{
	[Export] public PackedScene PoisonSpitScene;
	[Export] public float LightningInterval = 2.2f;
	[Export] public float PoisonInterval = 2.6f;
	[Export] public Vector2 PoisonLaunchVelocity = new(-160f, -160f);

	private Flamethrower _flamethrower;
	private Marker2D _lightningFrom;
	private Marker2D _lightningTo;
	private Marker2D _poisonOrigin;
	private float _lightningTimer;
	private float _poisonTimer;

	public override void _Ready()
	{
		_flamethrower = GetNode<Flamethrower>("Flamethrower");
		_lightningFrom = GetNode<Marker2D>("LightningFrom");
		_lightningTo = GetNode<Marker2D>("LightningTo");
		_poisonOrigin = GetNode<Marker2D>("PoisonOrigin");

		_flamethrower.SetActive(true);
		_lightningTimer = 1f;
		_poisonTimer = 1.6f;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		_lightningTimer -= dt;
		if (_lightningTimer <= 0f)
		{
			_lightningTimer = LightningInterval;
			LightningBolt.SpawnStrike(this, _lightningFrom.GlobalPosition, _lightningTo.GlobalPosition);
		}

		_poisonTimer -= dt;
		if (_poisonTimer <= 0f)
		{
			_poisonTimer = PoisonInterval;
			SpawnPoisonSpit();
		}
	}

	private void SpawnPoisonSpit()
	{
		if (PoisonSpitScene is null)
			return;

		var spit = PoisonSpitScene.Instantiate<PoisonSpit>();
		AddChild(spit);
		spit.GlobalPosition = _poisonOrigin.GlobalPosition;
		spit.Launch(PoisonLaunchVelocity, null);
	}
}
