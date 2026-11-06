using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Bandido de cuerda: a knife-thrower who waits hanging from one of the mine's swinging ropes
// (RopePath → a Rope). Made for La Mina: he rides the rope's own swing (same fixed sweep the player
// rides, so his position is readable), and from up there:
//  - Throws a single aimed knife every HangThrowCooldown while you're within HangThrowRange — the
//    arm winds up over "colgado_lanza" frames 0-4 (tell), the knife leaves on HangReleaseFrame.
//    Parry sends it back (ElfArrow handles reflection).
//  - Lets go and drops on you when you pass under him (within DropRange horizontally and below),
//    or when he's hit HitsToDrop times — knocked off the rope. While hanging he's a target you
//    reach from a neighbouring rope or with an up-attack; over acid, the drop can kill him.
// Once on the ground (or dead) he's a normal Bandido6Cuchillos: knife fans + escape hop.
// Art: PixelLab hang pose + sway/throw built from Bandido6Cuchillos's frames
// (assets/external/Chars/PixelLab/Bandidos/BandidoCuerda/).
public partial class BandidoCuerda : Bandido6Cuchillos
{
	[Export] public NodePath RopePath;
	[Export] public float HangThrowRange = 340f;
	[Export] public float HangThrowCooldown = 2.2f;
	[Export] public int HangReleaseFrame = 5;
	[Export] public float HangKnifeSpeed = 380f;
	[Export] public float DropRange = 48f;
	[Export] public int HitsToDrop = 2;
	// Body origin below the rope tip (the hand is at the top of the 64 px sprite, scale 1.4).
	[Export] public float HangOffsetY = 44f;

	private Rope _rope;
	private bool _hanging;
	private float _throwTimer = 1.2f;
	private bool _throwing;
	private bool _released;
	private int _hitsTaken;

	public override void _Ready()
	{
		base._Ready();
		if (Engine.IsEditorHint())
			return;
		_rope = GetNodeOrNull<Rope>(RopePath);
		_hanging = _rope != null;
		Stats.Died += LetGo;
		Stats.HitTaken += _ =>
		{
			if (_hanging && ++_hitsTaken >= HitsToDrop)
				LetGo();
		};
		if (_hanging && Sprite != null)
			Sprite.Play("colgado");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_hanging)
		{
			base._PhysicsProcess(delta);
			return;
		}
		if (IsQueuedForRemoval)
			return;

		float dt = (float)delta;
		GlobalPosition = _rope.EndPosition + new Vector2(0f, HangOffsetY);
		Velocity = Vector2.Zero;

		Node2D player = PlayerReads.Find(this);
		if (player == null)
			return;
		float dx = player.GlobalPosition.X - GlobalPosition.X;
		float dy = player.GlobalPosition.Y - GlobalPosition.Y;
		FacingRight = dx >= 0f;
		if (Visual != null)
			Visual.Scale = new Vector2(FacingRight ? 1 : -1, Visual.Scale.Y);

		// Right under him: let go and fall on the player.
		if (Mathf.Abs(dx) < DropRange && dy > 20f && dy < 400f)
		{
			LetGo();
			return;
		}

		_throwTimer -= dt;
		if (_throwing)
		{
			if (!_released && Sprite != null && Sprite.Frame >= HangReleaseFrame)
			{
				_released = true;
				ThrowOne(player);
			}
			if (Sprite == null || !Sprite.IsPlaying())
			{
				_throwing = false;
				Sprite?.Play("colgado");
			}
		}
		else if (_throwTimer <= 0f && new Vector2(dx, dy).Length() < HangThrowRange)
		{
			_throwTimer = HangThrowCooldown;
			_throwing = true;
			_released = false;
			Sprite?.Play("colgado_lanza");
		}
	}

	private void ThrowOne(Node2D player)
	{
		if (KnifeScene is null)
			return;
		Vector2 origin = GlobalPosition + new Vector2(FacingRight ? 16f : -16f, -6f);
		Vector2 dir = (player.GlobalPosition + new Vector2(0f, -10f) - origin).Normalized();
		var knife = KnifeScene.Instantiate<ElfArrow>();
		knife.Speed = HangKnifeSpeed;
		knife.DamageMultiplier = KnifeDamageMultiplier;
		GetTree().CurrentScene.AddChild(knife);
		knife.GlobalPosition = origin;
		knife.Launch(dir, Stats);
		Sfx.PlayAt(this, "Combat/Bow", "Bow Attack");
	}

	// Drops off the rope keeping the swing's sideways speed; the base AI takes over from here
	// (gravity, landing, fighting — or the acid).
	private void LetGo()
	{
		if (!_hanging)
			return;
		_hanging = false;
		_throwing = false;
		Velocity = new Vector2(_rope.EndVelocity.X * 0.6f, 0f);
		if (Sprite != null && Sprite.Animation.ToString().StartsWith("colgado"))
			Sprite.Play(Sprite.SpriteFrames.HasAnimation("hurt") ? "hurt" : "idle");
	}
}
