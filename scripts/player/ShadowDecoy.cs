using System.Collections.Generic;
using Godot;

namespace Metroidvania.Player;

// Clon señuelo (DS1's lure skull): a shadow orb thrown forward on an arc; where it lands it becomes
// a neon silhouette of the player that draws nearby enemies to its spot for Duration (see
// Enemy._PhysicsProcess — lured enemies walk over and ignore the player). With ExplosionRadius > 0
// (+1/+2) it bursts at the end, catching whoever gathered around it. Built in code — see Setup().
public partial class ShadowDecoy : Node2D
{
	private static readonly List<ShadowDecoy> Active = new();

	private SpriteFrames _frames;
	private string _idleAnimation;
	private bool _facingRight;
	private Vector2 _spritePosition;
	private Vector2 _spriteOffset;
	private Vector2 _spriteScale;
	private Color _tint;
	private float _intensity;
	private float _alpha;
	private Vector2 _velocity;
	private float _gravity;
	private float _duration;
	private float _lureRadius;
	private float _explosionRadius;
	private int _explosionDamage;

	private Sprite2D _orb;
	private bool _landed;
	private float _timeLeft;
	private float _flightTime;

	public Vector2 LurePoint => GlobalPosition;
	// Set by the caster: how far above the feet the player's body origin sits (the silhouette and
	// the burst are drawn from there).
	public float FeetOffset { get; set; }

	public void Setup(SpriteFrames frames, string idleAnimation, bool facingRight, Vector2 spritePosition, Vector2 spriteOffset,
		Vector2 spriteScale, Color tint, float intensity, float alpha, Vector2 launchVelocity, float gravity, float duration,
		float lureRadius, float explosionRadius, int explosionDamage)
	{
		_frames = frames;
		_idleAnimation = idleAnimation;
		_facingRight = facingRight;
		_spritePosition = spritePosition;
		_spriteOffset = spriteOffset;
		_spriteScale = spriteScale;
		_tint = tint;
		_intensity = intensity;
		_alpha = alpha;
		_velocity = launchVelocity;
		_gravity = gravity;
		_duration = duration;
		_lureRadius = lureRadius;
		_explosionRadius = explosionRadius;
		_explosionDamage = explosionDamage;
	}

	// The closest active (landed) decoy whose lure reaches `from`, if any.
	public static bool TryGetLure(Vector2 from, out Vector2 point)
	{
		point = default;
		float closest = float.MaxValue;
		foreach (ShadowDecoy decoy in Active)
		{
			if (!IsInstanceValid(decoy))
				continue;
			float distance = decoy.GlobalPosition.DistanceTo(from);
			if (distance <= decoy._lureRadius && distance < closest)
			{
				closest = distance;
				point = decoy.GlobalPosition;
			}
		}
		return closest < float.MaxValue;
	}

	public override void _Ready()
	{
		_orb = new Sprite2D
		{
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
			Modulate = _tint,
			Scale = Vector2.One * 0.3f,
		};
		AddChild(_orb);
	}

	public override void _ExitTree() => Active.Remove(this);

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (!_landed)
		{
			Fly(dt);
			return;
		}
		_timeLeft -= dt;
		if (_timeLeft <= 0f)
			End();
	}

	private void Fly(float dt)
	{
		_flightTime += dt;
		_velocity.Y += _gravity * dt;
		Vector2 next = GlobalPosition + _velocity * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, next, 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count == 0 && _flightTime < 2f)
		{
			GlobalPosition = next;
			return;
		}
		if (hit.Count > 0)
		{
			Vector2 normal = (Vector2)hit["normal"];
			// Off a wall it just drops where it struck; on a floor it stands right there.
			GlobalPosition = (Vector2)hit["position"] + normal * 2f;
			if (normal.Y > -0.5f)
			{
				_velocity = new Vector2(0f, Mathf.Max(0f, _velocity.Y));
				return;
			}
		}
		Land();
	}

	private void Land()
	{
		_landed = true;
		_timeLeft = _duration;
		_orb.QueueFree();
		Active.Add(this);

		// Standing silhouette, feet on the landing point: the player's sprite sits relative to its
		// body origin, which is BodyHalfHeight above the feet.
		var visual = new Node2D { Scale = new Vector2(_facingRight ? 1 : -1, 1), Position = new Vector2(0f, -FeetOffset) };
		AddChild(visual);
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = _frames,
			Position = _spritePosition,
			Offset = _spriteOffset,
			Scale = _spriteScale,
			Material = NeonSilhouette.CreateMaterial(_tint, _intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
		};
		visual.AddChild(sprite);
		sprite.Play(_idleAnimation);
		Tween appear = CreateTween();
		appear.TweenProperty(sprite, "modulate:a", _alpha, 0.12f);
		// A slow pulse so it reads as bait rather than a second player.
		Tween pulse = sprite.CreateTween().SetLoops();
		pulse.TweenProperty(sprite, "modulate:a", _alpha * 0.55f, 0.45f);
		pulse.TweenProperty(sprite, "modulate:a", _alpha, 0.45f);
	}

	private void End()
	{
		Active.Remove(this);
		SetPhysicsProcess(false);
		if (_explosionRadius > 0f)
		{
			ShadowBurst.Explode(this, GlobalPosition + new Vector2(0f, -FeetOffset), _explosionRadius, _explosionDamage, _tint);
			QueueFree();
			return;
		}
		Tween vanish = CreateTween();
		vanish.TweenProperty(this, "modulate:a", 0f, 0.3f);
		vanish.TweenCallback(Callable.From(QueueFree));
	}
}
