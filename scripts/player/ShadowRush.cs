using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.World;

namespace Metroidvania.Player;

// Clon embestida: a neon silhouette of the player that shoots forward along a straight line in its
// dash pose, cutting every enemy it passes through (one hit each, caster's AttackPower), and ends
// on a wall or after MaxDistance. With meteorHits > 0 (+1, "Meteoros de Pegaso") it instead stops
// on the first enemy it touches and unloads a rapid flurry on that one enemy — meteorHits small hits
// over meteorDuration, each a short neon streak at a random angle. Built in code — see Setup().
public partial class ShadowRush : Node2D
{
	private const string HitSparkFrames = "res://resources/sprites/HitSparkSpriteFrames.tres";

	private AnimatedSprite2D _sprite;
	private Hitbox _hitbox;
	private Stats _casterStats;
	private Color _tint;
	private float _alpha;
	private float _direction;
	private float _speed;
	private float _maxDistance;
	private float _fadeIn;
	private float _fadeOut;
	private int _meteorHits;
	private float _meteorDuration;
	private int _meteorDamage;
	private float _meteorRange;
	private float _ghostTimer;

	private float _travelled;
	private bool _rushing = true;
	private Node2D _meteorTarget;

	public void Setup(SpriteFrames frames, bool facingRight, Vector2 spritePosition, Vector2 spriteDrawOffset, Vector2 spriteScale,
		Stats casterStats, Color tint, float intensity, float alpha, Vector2 hitboxSize, float speed, float maxDistance,
		float fadeIn, float fadeOut, int meteorHits, float meteorDuration, int meteorDamage, float meteorRange)
	{
		_casterStats = casterStats;
		_tint = tint;
		_alpha = alpha;
		_direction = facingRight ? 1f : -1f;
		_speed = speed;
		_maxDistance = maxDistance;
		_fadeIn = fadeIn;
		_fadeOut = fadeOut;
		_meteorHits = meteorHits;
		_meteorDuration = meteorDuration;
		_meteorDamage = meteorDamage;
		_meteorRange = meteorRange;

		var visual = new Node2D { Scale = new Vector2(_direction, 1) };
		AddChild(visual);
		_sprite = new AnimatedSprite2D
		{
			SpriteFrames = frames,
			Position = spritePosition,
			Offset = spriteDrawOffset,
			Scale = spriteScale,
			Material = NeonSilhouette.CreateMaterial(tint, intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		visual.AddChild(_sprite);

		// Centred on the body: the rush itself is the blade. The flurry wants its target to stay put,
		// so no shove when it's going to stop there.
		_hitbox = new Hitbox { CollisionLayer = 0, CollisionMask = 5, KnockbackForce = meteorHits > 0 ? 0f : 160f };
		_hitbox.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new RectangleShape2D { Size = hitboxSize }, Disabled = true });
		AddChild(_hitbox);
	}

	public override void _Ready()
	{
		_hitbox.BodyEntered += OnBodyEntered;
		_sprite.Play(_sprite.SpriteFrames.HasAnimation("dash") ? "dash" : "run");
		CreateTween().TweenProperty(_sprite, "modulate:a", _alpha, _fadeIn);
		_hitbox.Activate(_casterStats);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_rushing)
			return;
		float dt = (float)delta;
		float step = _speed * dt;
		Vector2 next = GlobalPosition + new Vector2(_direction * step, 0f);
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next + new Vector2(_direction * 10f, 0f), 1u);
		if (GetWorld2D().DirectSpaceState.IntersectRay(query).Count > 0)
		{
			EndRush();
			return;
		}
		GlobalPosition = next;
		_travelled += step;

		_ghostTimer -= dt;
		if (_ghostTimer <= 0f)
		{
			_ghostTimer = 0.03f;
			SpawnGhost();
		}

		if (_travelled >= _maxDistance)
			EndRush();
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!_rushing || _meteorHits <= 0 || body.GetNodeOrNull<Stats>("Stats") is not { CurrentHealth: > 0 } stats || stats == _casterStats)
			return;
		_meteorTarget = body;
		_rushing = false;
		_hitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		_ = RunMeteors();
	}

	private void EndRush()
	{
		_rushing = false;
		_hitbox.Deactivate();
		_ = Vanish();
	}

	// Meteoros de Pegaso: the clone plants itself and alternates attack1/attack2 at high speed while
	// meteorHits hits land on the target, one streak each.
	private async Task RunMeteors()
	{
		_sprite.SpeedScale = 3f;
		float interval = _meteorDuration / _meteorHits;
		for (int i = 0; i < _meteorHits; i++)
		{
			string swing = i % 2 == 0 ? "attack1" : "attack2";
			if (_sprite.SpriteFrames.HasAnimation(swing))
				_sprite.Play(swing);

			if (IsInstanceValid(_meteorTarget) && _meteorTarget.IsInsideTree()
				&& _meteorTarget.GetNodeOrNull<Stats>("Stats") is { CurrentHealth: > 0 } stats
				&& _meteorTarget.GlobalPosition.DistanceTo(GlobalPosition) <= _meteorRange)
			{
				stats.TakeDamage(_meteorDamage, ignoreInvulnerability: true, armInvulnerability: false);
				(_meteorTarget as Metroidvania.World.Enemy)?.AddShadowMark();
				Vector2 at = _meteorTarget.GlobalPosition + new Vector2((float)GD.RandRange(-10.0, 10.0), (float)GD.RandRange(-22.0, 4.0));
				SpawnStreak(at);
				if (i % 3 == 0)
					VfxSpawner.SpawnAt(this, at, HitSparkFrames, "spark", new Vector2(0f, 10f));
			}

			await ToSignal(GetTree().CreateTimer(interval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}
		_sprite.SpeedScale = 1f;
		await Vanish();
	}

	// One meteor: a short additive neon dash through the hit point, mostly pointing away from the
	// clone, flashing in and out.
	private void SpawnStreak(Vector2 at)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		float angle = (float)GD.RandRange(-0.7, 0.7) + (_direction > 0f ? 0f : Mathf.Pi);
		Vector2 half = Vector2.FromAngle(angle) * (float)GD.RandRange(9.0, 15.0);
		var streak = new Node2D { ZIndex = ZIndex + 1 };
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		streak.AddChild(new Line2D { Points = new[] { -half, half }, Width = 6f, DefaultColor = _tint with { A = 0.6f }, Material = additive, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round });
		streak.AddChild(new Line2D { Points = new[] { -half, half }, Width = 2f, DefaultColor = _tint.Lightened(0.7f), Material = additive });
		scene.AddChild(streak);
		streak.GlobalPosition = at;
		Tween fade = streak.CreateTween();
		fade.TweenProperty(streak, "modulate:a", 0f, 0.14f).SetEase(Tween.EaseType.In);
		fade.TweenCallback(Callable.From(streak.QueueFree));
	}

	private void SpawnGhost()
	{
		Texture2D frame = _sprite.SpriteFrames.GetFrameTexture(_sprite.Animation, _sprite.Frame);
		if (frame is null || GetTree().CurrentScene is not Node scene)
			return;
		var ghost = new Sprite2D
		{
			Texture = frame,
			Centered = _sprite.Centered,
			Offset = _sprite.Offset,
			Material = _sprite.Material,
			Modulate = new Color(1f, 1f, 1f, _alpha * 0.45f),
			ZIndex = ZIndex - 1,
		};
		scene.AddChild(ghost);
		ghost.GlobalTransform = _sprite.GlobalTransform;
		Tween fade = ghost.CreateTween();
		fade.TweenProperty(ghost, "modulate:a", 0f, 0.22f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}

	private async Task Vanish()
	{
		_hitbox.Deactivate();
		Tween vanish = CreateTween();
		vanish.TweenProperty(_sprite, "modulate:a", 0f, _fadeOut).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		await ToSignal(vanish, Tween.SignalName.Finished);
		if (IsInstanceValid(this))
			QueueFree();
	}
}
