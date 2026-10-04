using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.Player;

// Bastón summon: a neon shadow of a defeated enemy type that appears, flashes over to the nearest
// enemy, throws that enemy's own attack (its real animation) once, and vanishes. Works for any enemy
// scene without per-enemy setup: the look (SpriteFrames, sprite offset/scale/flip, body size) is read
// from the enemy's scene once and cached.
public partial class ShadowSummon : Node2D
{
	private sealed class Template
	{
		public SpriteFrames Frames;
		public Vector2 SpritePosition;
		public Vector2 SpriteScale;
		public Vector2 SpriteOffset;
		public bool SpriteFlipH;
		public float BodyBottom;
		public string AttackAnimation;
		public float AttackLength;
		// >1 speeds up a long attack clip so every summon strikes snappily.
		public float SpeedScale = 1f;
	}

	private static readonly Dictionary<string, Template> Templates = new();
	// Longest the whole summon attack may take; longer enemy clips are played faster to fit.
	private const float MaxAttackLength = 0.8f;

	private string _enemyScenePath;
	private Stats _ownerStats;
	private int _damage;
	private bool _facingRight;
	private float _searchRadius;
	private float _reach;
	private Color _color;
	private float _intensity;

	public void Setup(string enemyScenePath, Stats ownerStats, int damage, bool facingRight, float searchRadius, float reach,
		Color color, float intensity)
	{
		_enemyScenePath = enemyScenePath;
		_ownerStats = ownerStats;
		_damage = damage;
		_facingRight = facingRight;
		_searchRadius = searchRadius;
		_reach = reach;
		_color = color;
		_intensity = intensity;
	}

	// Reads (and caches) how this enemy type looks and attacks, from an un-entered instance of its scene.
	private static Template GetTemplate(string scenePath)
	{
		if (Templates.TryGetValue(scenePath, out Template cached))
			return cached;
		if (!ResourceLoader.Exists(scenePath) || GD.Load<PackedScene>(scenePath)?.Instantiate() is not Node2D instance)
			return null;

		Template template = null;
		if (instance.GetNodeOrNull<AnimatedSprite2D>("Visual/CharacterSprite") is { SpriteFrames: not null } sprite)
		{
			float bottom = 30f;
			if (instance.GetNodeOrNull<CollisionShape2D>("CollisionShape2D") is { Shape: RectangleShape2D rect } shape)
				bottom = shape.Position.Y + rect.Size.Y / 2f;
			string attack = PickAttack(sprite.SpriteFrames);
			float length = 0.5f;
			if (attack is not null)
			{
				float speed = Mathf.Max(1f, (float)sprite.SpriteFrames.GetAnimationSpeed(attack));
				length = sprite.SpriteFrames.GetFrameCount(attack) / speed;
			}
			template = new Template
			{
				Frames = sprite.SpriteFrames,
				SpritePosition = sprite.Position,
				SpriteScale = sprite.Scale,
				SpriteOffset = sprite.Offset,
				SpriteFlipH = sprite.FlipH,
				BodyBottom = bottom,
				AttackAnimation = attack,
				AttackLength = Mathf.Clamp(length, 0.25f, MaxAttackLength),
				SpeedScale = length > MaxAttackLength ? length / MaxAttackLength : 1f,
			};
		}
		instance.Free();
		if (template is not null)
			Templates[scenePath] = template;
		return template;
	}

	private static string PickAttack(SpriteFrames frames)
	{
		foreach (string name in new[] { "attack", "attack1", "attack2", "Attack", "attack_1" })
			if (frames.HasAnimation(name))
				return name;
		foreach (StringName name in frames.GetAnimationNames())
			if (name.ToString().ToLowerInvariant().Contains("attack"))
				return name;
		return null;
	}

	// Deferred: the caster sets GlobalPosition right after adding it, so the target search must wait.
	public override void _Ready() => CallDeferred(MethodName.Begin);

	private async void Begin()
	{
		Template template = GetTemplate(_enemyScenePath);
		if (template is null)
		{
			QueueFree();
			return;
		}

		// Target: nearest living enemy in range, else strike where the caster faces.
		Node2D target = null;
		float best = _searchRadius;
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is Node2D enemy && enemy.GetNodeOrNull<Stats>("Stats") is { CurrentHealth: > 0 })
			{
				float distance = enemy.GlobalPosition.DistanceTo(GlobalPosition);
				if (distance < best)
				{
					best = distance;
					target = enemy;
				}
			}
		}

		bool faceRight = target is not null ? target.GlobalPosition.X >= GlobalPosition.X : _facingRight;
		Vector2 start = GlobalPosition;
		if (target is not null)
		{
			// Stand just short of the target, feet on the floor under it.
			float side = faceRight ? -1f : 1f;
			float floorY = FloorBelow(target.GlobalPosition) ?? target.GlobalPosition.Y + 30f;
			GlobalPosition = new Vector2(target.GlobalPosition.X + side * _reach * 0.6f, floorY - template.BodyBottom);
		}

		var visual = new Node2D { Scale = new Vector2(faceRight ? 1f : -1f, 1f) };
		AddChild(visual);
		var sprite = new AnimatedSprite2D
		{
			SpriteFrames = template.Frames,
			Position = template.SpritePosition,
			Scale = template.SpriteScale,
			Offset = template.SpriteOffset,
			FlipH = template.SpriteFlipH,
			Material = NeonSilhouette.CreateMaterial(_color, _intensity),
			Modulate = new Color(1f, 1f, 1f, 0f),
			SpeedScale = template.SpeedScale,
		};
		visual.AddChild(sprite);

		// Streak from the caster to where it appears.
		if (target is not null && GetParent() is Node parent)
		{
			var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
			var streak = new Line2D { Points = new[] { start, GlobalPosition }, Width = 6f, Material = additive, DefaultColor = _color };
			parent.AddChild(streak);
			Tween fadeStreak = streak.CreateTween();
			fadeStreak.TweenProperty(streak, "modulate:a", 0f, 0.25f);
			fadeStreak.TweenCallback(Callable.From(streak.QueueFree));
		}

		Tween appear = CreateTween();
		appear.TweenProperty(sprite, "modulate:a", 0.9f, 0.08f);
		if (template.AttackAnimation is not null)
			sprite.Play(template.AttackAnimation);
		else
			sprite.Play(template.Frames.GetAnimationNames()[0]);

		// The hit lands a bit before the middle of the swing.
		await ToSignal(GetTree().CreateTimer(template.AttackLength * 0.45f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		if (IsInstanceValid(target) && target.GetNodeOrNull<Stats>("Stats") is { CurrentHealth: > 0 } targetStats
			&& Mathf.Abs(target.GlobalPosition.X - GlobalPosition.X) <= _reach * 1.5f)
			targetStats.TakeDamage(_damage, ignoreInvulnerability: true);

		await ToSignal(GetTree().CreateTimer(template.AttackLength * 0.55f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this))
			return;
		Tween vanish = CreateTween();
		vanish.TweenProperty(sprite, "modulate:a", 0f, 0.2f);
		vanish.TweenCallback(Callable.From(QueueFree));
	}

	private float? FloorBelow(Vector2 from)
	{
		var query = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0f, 300f), 1u);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		return hit.Count > 0 ? ((Vector2)hit["position"]).Y : null;
	}
}
