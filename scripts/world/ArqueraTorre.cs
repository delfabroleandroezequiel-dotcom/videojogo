using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Arquera en la torre: an ally that stands on a high spot (Alathea on BosqueLobos1's watchtower
// during "El carro de Morlok") and shoots the nearest enemy in range with her bow. She never moves;
// her barks are a VozAmbiente child. With QuestId set she only exists while that quest is active
// (started and not completed), so she's there for the fight and gone afterwards.
//
// The shot follows the art: SpriteFrames' "attack1" is a full draw-and-release; the arrow leaves on
// ReleaseFrame. The arrow is procedural (a golden streak) and only hits bodies on the enemy layer.
public partial class ArqueraTorre : Node2D
{
	[Export] public SpriteFrames Frames;
	[Export] public Vector2 SpriteScale = new(1.158f, 1.158f);
	[Export] public string IdleAnimation = "idle";
	[Export] public string ShootAnimation = "attack1";
	[Export] public int ReleaseFrame = 7;
	// Bow height and reach in front of her, in node space while facing right.
	[Export] public Vector2 ArrowOrigin = new(26f, -6f);
	[Export] public float Range = 720f;
	[Export] public float Cooldown = 1.6f;
	[Export] public int Damage = 34;
	[Export] public float ArrowSpeed = 620f;
	[Export] public Color ArrowColor = new(1f, 0.85f, 0.45f, 1f);
	[Export] public string QuestId = "";
	// Only exists while this boss is alive, and slips away when it dies (Alathea on a ledge over
	// the spider). Empty = no boss gate.
	[Export] public string BossAliveId = "";
	// After this many arrows she leaves (fades out); 0 = no limit. The hunting-horn call uses it so
	// she eases a fight instead of winning it.
	[Export] public int MaxShots;
	// Line she says when the BossAliveId boss dies, before fading out (Alathea over the dead spider).
	// Empty = she just fades, as before.
	[Export] public string DespedidaKey = "";
	[Export] public float DespedidaSubtitleHeight = 50f;

	private AnimatedSprite2D _sprite;
	private int _shotsFired;
	private bool _leaving;
	private float _cooldown = 0.8f;
	private bool _shooting;
	private Node2D _target;

	public override void _Ready()
	{
		if (!string.IsNullOrEmpty(QuestId) && Metroidvania.Quests.QuestManager.Instance is { } quests
			&& (!quests.IsActive(QuestId) || quests.IsCompleted(QuestId)))
		{
			QueueFree();
			return;
		}
		if (!string.IsNullOrEmpty(BossAliveId) && Metroidvania.Save.SaveManager.Instance.IsBossDefeated(BossAliveId))
		{
			QueueFree();
			return;
		}
		_sprite = new AnimatedSprite2D { SpriteFrames = Frames, Scale = SpriteScale, TextureFilter = TextureFilterEnum.Nearest };
		AddChild(_sprite);
		MoveChild(_sprite, 0);
		_sprite.Play(IdleAnimation);
		_sprite.FrameChanged += OnFrameChanged;
		_sprite.AnimationFinished += () =>
		{
			if (_sprite.Animation == ShootAnimation)
			{
				_shooting = false;
				_sprite.Play(IdleAnimation);
			}
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_sprite == null || _shooting || _leaving)
			return;
		if ((MaxShots > 0 && _shotsFired >= MaxShots)
			|| (!string.IsNullOrEmpty(BossAliveId) && Metroidvania.Save.SaveManager.Instance.IsBossDefeated(BossAliveId)))
		{
			Leave();
			return;
		}
		_cooldown -= (float)delta;
		if (_cooldown > 0f)
			return;
		_target = NearestEnemy();
		if (_target == null)
			return;
		_cooldown = Cooldown;
		_shooting = true;
		_sprite.FlipH = _target.GlobalPosition.X < GlobalPosition.X;
		_sprite.Play(ShootAnimation);
	}

	private Node2D NearestEnemy()
	{
		Node2D best = null;
		float bestDist = Range;
		foreach (Node n in GetTree().GetNodesInGroup("enemy"))
		{
			if (n is not Node2D e || !IsInstanceValid(e) || e.GetNodeOrNull<Stats>("Stats") is not { } st || st.CurrentHealth <= 0)
				continue;
			float d = e.GlobalPosition.DistanceTo(GlobalPosition);
			if (d < bestDist)
			{
				bestDist = d;
				best = e;
			}
		}
		return best;
	}

	private void OnFrameChanged()
	{
		if (_sprite.Animation != ShootAnimation || _sprite.Frame != ReleaseFrame)
			return;
		Vector2 origin = GlobalPosition + new Vector2(_sprite.FlipH ? -ArrowOrigin.X : ArrowOrigin.X, ArrowOrigin.Y);
		Vector2 aim = IsInstanceValid(_target) ? _target.GlobalPosition + new Vector2(0f, -8f) : origin + new Vector2(_sprite.FlipH ? -1f : 1f, 0f);
		var arrow = new FlechaAliada { Direction = (aim - origin).Normalized(), Speed = ArrowSpeed, Damage = Damage, Tint = ArrowColor };
		GetTree().CurrentScene.AddChild(arrow);
		arrow.GlobalPosition = origin;
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
		_shotsFired++;
	}

	// Fades out and frees: out of arrows, or the boss she came for is dead.
	private void Leave()
	{
		_leaving = true;
		float wait = 0.6f;
		bool bossDead = !string.IsNullOrEmpty(BossAliveId) && Metroidvania.Save.SaveManager.Instance.IsBossDefeated(BossAliveId);
		if (bossDead && !string.IsNullOrEmpty(DespedidaKey))
		{
			// No more fight barks over the farewell.
			foreach (Node child in GetChildren())
				if (child is VozAmbiente voz)
					voz.LineKeys = System.Array.Empty<string>();
			AudioStream stream = Sfx.LoadVoice(DespedidaKey);
			float length = stream is null ? 2.5f : (float)stream.GetLength();
			if (stream is not null)
			{
				var player = new AudioStreamPlayer2D { Stream = stream, MaxDistance = 1400f, Attenuation = 1.2f, PanningStrength = 0.5f, VolumeDb = -4f };
				AddChild(player);
				player.Position = new Vector2(0f, -40f);
				player.Play();
			}
			FloatingSubtitle.Show(this, DespedidaKey, length, DespedidaSubtitleHeight);
			wait = length + 0.3f;
		}
		Tween tween = CreateTween();
		tween.TweenInterval(wait);
		tween.TweenProperty(this, "modulate:a", 0f, 0.8f);
		tween.TweenCallback(Callable.From(QueueFree));
	}

	// Fades her in where she's called (the hunting horn), instead of popping into place.
	public void FadeIn()
	{
		Modulate = new Color(1f, 1f, 1f, 0f);
		CreateTween().TweenProperty(this, "modulate:a", 1f, 0.5f);
	}
}

// A straight golden arrow that only hits enemies (layer 4), once, then vanishes.
public partial class FlechaAliada : Area2D
{
	public Vector2 Direction = Vector2.Right;
	public float Speed = 620f;
	public int Damage = 34;
	public Color Tint = Colors.White;
	private float _life = 1.6f;
	private bool _done;

	public override void _Ready()
	{
		CollisionLayer = 0;
		CollisionMask = 4;
		Rotation = Direction.Angle();
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(18f, 4f) } });
		AddChild(new Line2D
		{
			Points = new[] { new Vector2(-16f, 0f), new Vector2(8f, 0f) },
			Width = 2f,
			DefaultColor = Tint,
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
		});
		BodyEntered += OnBody;
	}

	public override void _PhysicsProcess(double delta)
	{
		Position += Direction * Speed * (float)delta;
		_life -= (float)delta;
		if (_life <= 0f)
			QueueFree();
	}

	private void OnBody(Node2D body)
	{
		if (_done)
			return;
		if (ProjectileReflect.TryHitEnemy(body, Damage, Direction, 120f))
		{
			_done = true;
			QueueFree();
		}
	}
}
