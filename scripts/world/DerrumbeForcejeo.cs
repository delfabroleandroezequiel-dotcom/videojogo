using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Sells a boss pinned under a rockslide (the crimson spider in SpiderBossArena, buried up to the
// abdomen under Pedrasboss): every few seconds it strains against the pile, which shudders and
// sheds pebbles and dust where it touches the body. The pile also gives way with the fight: a big
// rock breaks loose at 2/3 HP and the whole heap sags at 1/3, so by the time RockslideBlocker blows
// it up on the boss's death it was already coming down. Tug() is the strong version for a cinematic
// (the boss lunges at the player and the rocks yank it back).
//
// All procedural (Polygon2D pebbles + a soft dust texture), so it matches any rock art. Put it as a
// child of the rockslide; ContactFrom/ContactTo are local points along the edge that touches the boss.
[GlobalClass]
public partial class DerrumbeForcejeo : Node2D
{
	[Export] public NodePath BossPath;
	[Export] public NodePath RockSpritePath = "../Sprite2D";
	[Export] public Texture2D DustTexture;

	[ExportGroup("Contact")]
	[Export] public Vector2 ContactFrom = new(-330, 90);
	[Export] public Vector2 ContactTo = new(-150, -60);
	// Global Y where debris hits the floor and settles.
	[Export] public float FloorY = 470f;
	[Export] public Color PebbleColor = new(0.22f, 0.21f, 0.22f);
	[Export] public Color DustColor = new(0.45f, 0.42f, 0.36f, 0.55f);

	[ExportGroup("Strain")]
	[Export] public float IntervalMin = 5f;
	[Export] public float IntervalMax = 9f;
	[Export] public int PebblesPerStrain = 5;
	[Export] public int DustPerStrain = 3;
	[Export] public float ShakePixels = 2f;
	// Pull of the boss's body toward the player on each strain (the lunge it can't finish).
	[Export] public float BodyJolt = 8f;

	[ExportGroup("Pile giving way")]
	[Export] public float BigRockAtHealthRatio = 0.66f;
	[Export] public float SagAtHealthRatio = 0.33f;
	[Export] public Vector2 BigRockFrom = new(-60, -200);
	[Export] public float BigRockSize = 34f;
	[Export] public Vector2 SagOffset = new(-4, 10);
	[Export(PropertyHint.Range, "-10,10")] public float SagDegrees = -2f;
	// Strains come this much more often once the heap has sagged.
	[Export] public float SaggedIntervalMultiplier = 0.6f;

	[ExportGroup("Sound")]
	[Export] public string StrainSoundCategory = "Footsteps/Stone";
	[Export] public string StrainSoundKey = "Stone Chain Land";
	[Export] public string RumbleSoundCategory = "Magic";
	[Export] public string RumbleSoundKey = "Rock Wall";

	private readonly RandomNumberGenerator _rng = new();
	private Enemy _boss;
	private Node2D _rocks;
	private Vector2 _rocksBase;
	private bool _bigRockDone;
	private bool _sagged;
	private bool _busy;

	public override void _Ready()
	{
		_rng.Randomize();
		_rocks = GetNodeOrNull<Node2D>(RockSpritePath);
		if (_rocks is not null)
			_rocksBase = _rocks.Position;

		_boss = BossPath is null || BossPath.IsEmpty ? null : GetNodeOrNull<Enemy>(BossPath);
		if (_boss is null || _boss.IsQueuedForDeletion() || _boss.Stats is null)
		{
			SetProcess(false);
			return;
		}
		_boss.Stats.HealthChanged += OnBossHealthChanged;
		_boss.Stats.Died += () => SetProcess(false);
		_ = StrainLoop();
	}

	private async Task StrainLoop()
	{
		while (IsInstanceValid(this) && IsInsideTree())
		{
			float wait = _rng.RandfRange(IntervalMin, IntervalMax) * (_sagged ? SaggedIntervalMultiplier : 1f);
			await ToSignal(GetTree().CreateTimer(wait, false), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || !IsInsideTree() || !BossAlive())
				return;
			Strain(1f);
		}
	}

	private bool BossAlive() => IsInstanceValid(_boss) && _boss.Stats is { CurrentHealth: > 0 } && !_released;

	// The boss tore itself out (SpiderBossArena phase 3): no more straining or jolting its body.
	private bool _released;
	public void Release() => _released = true;

	// One pull against the pile: rocks shudder, debris and dust come off the contact edge, the body
	// jerks forward and back. strength scales everything.
	public void Strain(float strength)
	{
		if (_busy)
			return;
		_ = ShudderRocks(ShakePixels * strength, 0.35f + 0.15f * strength);
		JoltBody(BodyJolt * strength, 0.1f, 0.22f);
		int pebbles = Mathf.RoundToInt(PebblesPerStrain * strength);
		for (int i = 0; i < pebbles; i++)
			SpawnPebble(_rng.RandfRange(2.5f, 5.5f) * Mathf.Sqrt(strength));
		int dust = Mathf.RoundToInt(DustPerStrain * strength);
		for (int i = 0; i < dust; i++)
			SpawnDust(0.6f + 0.4f * strength);
		if (!string.IsNullOrEmpty(StrainSoundKey))
			Sfx.PlayIfExistsAt(this, StrainSoundCategory, StrainSoundKey);
	}

	// The cinematic version: lunges at the player, the pile yanks it back hard.
	public void Tug()
	{
		Strain(2.2f);
		JoltBody(BodyJolt * 5f, 0.14f, 0.32f);
		ShakePlayerCamera(8f, 0.4f);
		if (!string.IsNullOrEmpty(RumbleSoundKey))
			Sfx.PlayIfExistsAt(this, RumbleSoundCategory, RumbleSoundKey);
	}

	private void OnBossHealthChanged(int current, int max)
	{
		if (max <= 0 || current <= 0)
			return;
		float ratio = (float)current / max;
		if (!_bigRockDone && ratio <= BigRockAtHealthRatio)
		{
			_bigRockDone = true;
			DropBigRock();
		}
		if (!_sagged && ratio <= SagAtHealthRatio)
		{
			_sagged = true;
			Sag();
		}
	}

	private void DropBigRock()
	{
		Strain(1.4f);
		var rock = new Debris
		{
			Polygon = RockShape(BigRockSize),
			Color = PebbleColor.Lightened(0.08f),
			Position = BigRockFrom,
			Velocity = new Vector2(_rng.RandfRange(-140f, -90f), -60f),
			Spin = _rng.RandfRange(-4f, -2f),
			FloorY = FloorY,
			Bounce = 0.25f,
			StaysOnFloor = true,
			OnLand = () => ShakePlayerCamera(5f, 0.3f),
			ZIndex = 2,
		};
		rock.Owner2 = this;
		AddChild(rock);
		if (!string.IsNullOrEmpty(RumbleSoundKey))
			Sfx.PlayIfExistsAt(this, RumbleSoundCategory, RumbleSoundKey);
	}

	private void Sag()
	{
		Strain(1.8f);
		if (_rocks is null)
			return;
		_rocksBase += SagOffset;
		Tween tween = CreateTween().SetParallel();
		tween.TweenProperty(_rocks, "position", _rocksBase, 0.7f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_rocks, "rotation_degrees", _rocks.RotationDegrees + SagDegrees, 0.7f);
		ShakePlayerCamera(6f, 0.45f);
	}

	private async Task ShudderRocks(float pixels, float duration)
	{
		if (_rocks is null || pixels <= 0f)
			return;
		_busy = true;
		float t = 0f;
		while (t < duration && IsInstanceValid(_rocks))
		{
			float fade = 1f - t / duration;
			_rocks.Position = _rocksBase + new Vector2(_rng.RandfRange(-pixels, pixels), _rng.RandfRange(-pixels, pixels) * 0.5f) * fade;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			t += (float)GetProcessDeltaTime();
		}
		if (IsInstanceValid(_rocks))
			_rocks.Position = _rocksBase;
		_busy = false;
	}

	// The boss's drawing lurches toward the player (it faces left) and snaps back; its body and AI
	// stay put, it's only the picture.
	private void JoltBody(float pixels, float outTime, float backTime)
	{
		if (_released || !IsInstanceValid(_boss) || pixels <= 0f || _boss.GetNodeOrNull<Node2D>("Visual") is not Node2D visual)
			return;
		Vector2 rest = visual.Position;
		Tween tween = visual.CreateTween();
		tween.TweenProperty(visual, "position", rest + new Vector2(-pixels, 0f), outTime).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(visual, "position", rest, backTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	private Vector2 RandomContactPoint() => ContactFrom.Lerp(ContactTo, _rng.Randf());

	private void SpawnPebble(float size)
	{
		var pebble = new Debris
		{
			Polygon = RockShape(size),
			Color = PebbleColor.Lightened(_rng.RandfRange(-0.05f, 0.1f)),
			Position = RandomContactPoint(),
			Velocity = new Vector2(_rng.RandfRange(-120f, -20f), _rng.RandfRange(-140f, -30f)),
			Spin = _rng.RandfRange(-8f, 8f),
			FloorY = FloorY,
			Bounce = 0.35f,
			ZIndex = 2,
		};
		pebble.Owner2 = this;
		AddChild(pebble);
	}

	private void SpawnDust(float scale) => SpawnDustAt(RandomContactPoint(), scale);

	private void SpawnDustAt(Vector2 localOrDebris, float scale)
	{
		if (DustTexture is null)
			return;
		var puff = new Sprite2D
		{
			Texture = DustTexture,
			Position = localOrDebris,
			Modulate = new Color(DustColor.R, DustColor.G, DustColor.B, 0f),
			Scale = Vector2.One * 0.4f * scale,
			ZIndex = 3,
		};
		AddChild(puff);
		Vector2 drift = new(_rng.RandfRange(-40f, -10f), _rng.RandfRange(-30f, -10f));
		Tween tween = puff.CreateTween().SetParallel();
		tween.TweenProperty(puff, "modulate:a", DustColor.A, 0.2f);
		tween.TweenProperty(puff, "scale", Vector2.One * 1.1f * scale, 1.4f).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(puff, "position", puff.Position + drift, 1.4f);
		tween.Chain().TweenProperty(puff, "modulate:a", 0f, 0.6f);
		tween.Chain().TweenCallback(Callable.From(puff.QueueFree));
	}

	private Vector2[] RockShape(float radius)
	{
		int sides = _rng.RandiRange(5, 7);
		var points = new Vector2[sides];
		for (int i = 0; i < sides; i++)
		{
			float angle = Mathf.Tau * i / sides + _rng.RandfRange(-0.25f, 0.25f);
			points[i] = Vector2.FromAngle(angle) * radius * _rng.RandfRange(0.7f, 1.1f);
		}
		return points;
	}

	private void ShakePlayerCamera(float strength, float duration)
	{
		if (GetTree().GetFirstNodeInGroup("player") is Metroidvania.Player.Player player)
			player.ShakeCamera(strength, duration);
	}

	// A falling bit of rock: simple gravity, a bounce or two on FloorY, then it fades (pebbles) or
	// stays as rubble (the big rock).
	private partial class Debris : Polygon2D
	{
		public DerrumbeForcejeo Owner2;
		public Vector2 Velocity;
		public float Spin;
		public float FloorY;
		public float Bounce;
		public bool StaysOnFloor;
		public System.Action OnLand;
		private int _bounces;
		private bool _settled;

		public override void _Process(double delta)
		{
			if (_settled)
				return;
			float dt = (float)delta;
			Velocity.Y += 900f * dt;
			Position += Velocity * dt;
			Rotation += Spin * dt;
			if (GlobalPosition.Y < FloorY || Velocity.Y < 0f)
				return;

			GlobalPosition = new Vector2(GlobalPosition.X, FloorY);
			if (_bounces == 0)
			{
				OnLand?.Invoke();
				if (OnLand is not null && IsInstanceValid(Owner2))
					Owner2.SpawnDustAt(Position, 1.6f);
			}
			_bounces++;
			if (_bounces <= 2 && Mathf.Abs(Velocity.Y) > 80f)
			{
				Velocity = new Vector2(Velocity.X * 0.5f, -Velocity.Y * Bounce);
				Spin *= 0.5f;
				return;
			}
			_settled = true;
			if (StaysOnFloor)
				return;
			Tween tween = CreateTween();
			tween.TweenInterval(1.5f);
			tween.TweenProperty(this, "modulate:a", 0f, 0.8f);
			tween.TweenCallback(Callable.From(QueueFree));
		}
	}
}
