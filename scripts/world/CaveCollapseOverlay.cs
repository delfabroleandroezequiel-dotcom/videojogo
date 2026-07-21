using Godot;

namespace Metroidvania.World;

// Full-screen falling-rock effect, hand-rolled instead of using a particle node (CPUParticles2D/
// GPUParticles2D aren't available in this project's C# bindings) — a small pool of Sprite2D
// "rocks" that fall, rotate, and recycle back to the top. Only active while a specific enemy has
// spotted the player (see Enemy.PlayerDetected), and jolts the player's camera the instant that
// starts, selling the idea that the enemy noticing you is what shakes the rocks loose.
public partial class CaveCollapseOverlay : CanvasLayer
{
	[Export] public NodePath TriggerEnemyPath;
	[Export] public float ShakeStrength = 6f;
	[Export] public float ShakeDuration = 0.35f;

	[Export] public Texture2D[] RockTextures = System.Array.Empty<Texture2D>();
	[Export] public int RockCount = 14;
	[Export] public float SpawnRatePerSecond = 7f;
	[Export] public float SpawnWidth = 1152f;
	[Export] public float SpawnYAbove = 20f;
	[Export] public float DespawnY = 720f;
	[Export] public float MinFallSpeed = 300f;
	[Export] public float MaxFallSpeed = 550f;
	[Export] public float FallGravity = 1200f;
	[Export] public float MinScale = 0.02f;
	[Export] public float MaxScale = 0.05f;
	[Export] public Color TintColor = new(0.45f, 0.42f, 0.4f, 1f);

	private struct Rock
	{
		public Sprite2D Sprite;
		public float VelocityY;
		public float AngularVelocity;
		public bool Active;
	}

	private Rock[] _rocks;
	private readonly RandomNumberGenerator _rng = new();
	private Enemy _triggerEnemy;
	private bool _emitting;
	private float _spawnTimer;

	public override void _Ready()
	{
		_rng.Randomize();

		if (TriggerEnemyPath is not null && !TriggerEnemyPath.IsEmpty)
			_triggerEnemy = GetNodeOrNull<Enemy>(TriggerEnemyPath);

		_rocks = new Rock[RockCount];
		for (int i = 0; i < RockCount; i++)
		{
			var sprite = new Sprite2D { Modulate = TintColor, Visible = false };
			AddChild(sprite);
			_rocks[i] = new Rock { Sprite = sprite, Active = false };
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		bool detected = _triggerEnemy is not null && _triggerEnemy.PlayerDetected;
		if (detected && !_emitting && GetTree().GetFirstNodeInGroup("player") is Player.Player player)
			player.ShakeCamera(ShakeStrength, ShakeDuration);
		_emitting = detected;

		if (_emitting && RockTextures.Length > 0)
		{
			_spawnTimer -= dt;
			if (_spawnTimer <= 0f)
			{
				_spawnTimer = 1f / SpawnRatePerSecond;
				TrySpawnRock();
			}
		}

		for (int i = 0; i < _rocks.Length; i++)
		{
			ref Rock rock = ref _rocks[i];
			if (!rock.Active)
				continue;

			rock.VelocityY += FallGravity * dt;
			Vector2 pos = rock.Sprite.Position;
			pos.Y += rock.VelocityY * dt;
			rock.Sprite.Position = pos;
			rock.Sprite.Rotation += rock.AngularVelocity * dt;

			if (pos.Y > DespawnY)
			{
				rock.Active = false;
				rock.Sprite.Visible = false;
			}
		}
	}

	private void TrySpawnRock()
	{
		for (int i = 0; i < _rocks.Length; i++)
		{
			if (_rocks[i].Active)
				continue;

			ref Rock rock = ref _rocks[i];
			rock.Sprite.Texture = RockTextures[_rng.RandiRange(0, RockTextures.Length - 1)];
			rock.Sprite.Position = new Vector2(_rng.RandfRange(0f, SpawnWidth), -SpawnYAbove);
			rock.Sprite.Scale = Vector2.One * _rng.RandfRange(MinScale, MaxScale);
			rock.Sprite.Rotation = _rng.RandfRange(0f, Mathf.Tau);
			rock.Sprite.Visible = true;
			rock.VelocityY = _rng.RandfRange(MinFallSpeed, MaxFallSpeed);
			rock.AngularVelocity = _rng.RandfRange(-6f, 6f);
			rock.Active = true;
			return;
		}
	}
}
