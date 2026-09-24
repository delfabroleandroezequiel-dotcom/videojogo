using Godot;
using Metroidvania.Player;

namespace Metroidvania.World;

// Rooted in place (set MoveSpeed = 0 on the scene/profile) — its threat is a telegraphed spore
// burst centered on itself rather than a chase: a green blink + neon ring marking the area, then the
// cloud, which poisons (damage over time) whoever is inside instead of a direct hit. The burst only becomes damaging partway through
// the release animation (once the cloud sprite has actually bloomed), matching what the player
// sees rather than punishing them the instant the animation starts.
public partial class MushroomEnemy : Enemy
{
	[Export] public float SporeRange = 90f;
	[Export] public float SporeCooldown = 2.2f;
	[Export] public float TelegraphDuration = 0.6f;
	[Export] public float BurstActiveDuration = 0.35f;
	[Export] public float ReleaseAnimDuration = 0.9f;
	[Export] public float HurtAnimDuration = 0.3f;
	[Export] public string SmokePoisonFramesPath = "res://resources/sprites/SmokePoisonSpriteFrames.tres";
	[Export] public float SmokePoisonScale = 0.7f;

	// On (default): the burst poisons (Player.ApplyPoison) instead of landing a direct hit.
	[Export] public bool PoisonInsteadOfHit = true;
	[Export] public float PoisonRadius = 52f;
	[Export] public int PoisonTickDamage = 8; // goes through Stats.TakeDamage, so the player's Defense (5) applies → 3 per tick
	[Export] public float PoisonTickInterval = 0.5f;
	[Export] public float PoisonDuration = 3f;
	// Tell: the mushroom blinks green and a neon ring on the ground shows the area the cloud will cover.
	[Export] public Color TellColor = new(0.3f, 1f, 0.45f, 1f);
	private Hitbox _hitbox;
	private bool _releasing;
	private bool _canRelease = true;
	private float _hurtTimer;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_hitbox = GetNode<Hitbox>("BurstHitbox");
		Stats.HitTaken += (isProjectile) => _hurtTimer = HurtAnimDuration;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);

		if (_hurtTimer > 0f)
			_hurtTimer -= (float)delta;

		if (_releasing || !_canRelease)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null || GlobalPosition.DistanceTo(player.GlobalPosition) > SporeRange)
			return;

		if (player is Metroidvania.Player.Player p && p.IsDashing)
			return;

		if (EnemyCombatCoordinator.TryAcquireAttackSlot())
			Release();
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (Sprite is null) return;
		string anim = _releasing ? "release" : (_hurtTimer > 0f ? "hurt" : "idle");
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
	}

	// Green blink on the mushroom + a pulsing neon ring (with light, for dark maps) marking the cloud's
	// reach, for the whole telegraph.
	private Node2D BuildTell()
	{
		var tell = new Node2D();
		AddChild(tell);
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		const int segments = 40;
		var points = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
			points[i] = Vector2.Right.Rotated(Mathf.Tau * i / segments) * (PoisonInsteadOfHit ? PoisonRadius : SporeRange * 0.6f);
		var ring = new Line2D { Points = points, Width = 3f, Material = additive, Modulate = TellColor, Scale = new Vector2(1f, 0.45f) };
		tell.AddChild(ring);
		tell.AddChild(new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = TellColor,
			Energy = 1f,
			TextureScale = 0.6f,
		});

		Tween pulse = tell.CreateTween().SetLoops();
		pulse.TweenProperty(ring, "modulate:a", 0.25f, 0.1f);
		pulse.TweenProperty(ring, "modulate:a", 1f, 0.1f);
		if (Sprite is not null)
		{
			Tween blink = Sprite.CreateTween().SetLoops(3);
			blink.TweenProperty(Sprite, "self_modulate", new Color(0.55f, 3f, 0.75f, 1f), TelegraphDuration / 6f);
			blink.TweenProperty(Sprite, "self_modulate", Colors.White, TelegraphDuration / 6f);
		}
		return tell;
	}

	private void PoisonPlayerInCloud()
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player)
			return;
		if (player.IsDashing || player.GlobalPosition.DistanceTo(GlobalPosition) > PoisonRadius + 16f)
			return;
		player.ApplyPoison(PoisonTickDamage, PoisonTickInterval, PoisonDuration);
	}

	private async void Release()
	{
		_releasing = true;
		_canRelease = false;

		try
		{
			Node2D tell = BuildTell();
			await ToSignal(GetTree().CreateTimer(TelegraphDuration), SceneTreeTimer.SignalName.Timeout);
			if (IsInstanceValid(tell))
				tell.QueueFree();
			if (Sprite is not null)
				Sprite.SelfModulate = Colors.White;
			if (!IsInstanceValid(this) || IsQueuedForRemoval)
				return;

			VfxSpawner.SpawnAt(this, GlobalPosition, SmokePoisonFramesPath, "smoke_poison", scale: SmokePoisonScale);
			if (PoisonInsteadOfHit)
				PoisonPlayerInCloud();
			else
				_hitbox.Activate(Stats);

			await ToSignal(GetTree().CreateTimer(BurstActiveDuration), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_hitbox.Deactivate();

			float remainingAnimTime = Mathf.Max(0f, ReleaseAnimDuration - TelegraphDuration - BurstActiveDuration);
			await ToSignal(GetTree().CreateTimer(remainingAnimTime), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			_releasing = false;
		}
		finally
		{
			EnemyCombatCoordinator.ReleaseAttackSlot();
		}

		await ToSignal(GetTree().CreateTimer(SporeCooldown), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_canRelease = true;
	}
}
