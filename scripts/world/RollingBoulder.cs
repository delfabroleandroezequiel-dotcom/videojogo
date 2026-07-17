using Godot;

namespace Metroidvania.World;

public partial class RollingBoulder : CharacterBody2D
{
	[Export] public float Speed = 260f;
	[Export] public int Direction = -1; // -1 = rolls left, 1 = rolls right
	[Export] public float Gravity = 900f;
	[Export] public float RespawnDelay = 1.2f;
	[Export] public float MaxAirTime = 10f;
	[Export] public PackedScene ExplosionScene;
	[Export] public float ExplosionScale = 1f;

	private Node2D _visual;
	private Area2D _hazardArea;
	private Vector2 _spawnPosition;
	private bool _exploding;
	private float _airTime;

	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_hazardArea = GetNode<Area2D>("HazardArea");
		_spawnPosition = Position;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_exploding)
			return;

		bool onFloor = IsOnFloor();
		Vector2 velocity = Velocity;
		velocity.Y = onFloor ? 0f : velocity.Y + Gravity * (float)delta;
		velocity.X = onFloor ? Direction * Speed : 0f;
		Velocity = velocity;

		MoveAndSlide();

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			Vector2 normal = GetSlideCollision(i).GetNormal();
			if (Mathf.Abs(normal.X) > Mathf.Abs(normal.Y))
			{
				Explode();
				return;
			}
		}

		_airTime = IsOnFloor() ? 0f : _airTime + (float)delta;
		if (_airTime >= MaxAirTime)
			Explode();
	}

	private void Explode()
	{
		_exploding = true;
		Velocity = Vector2.Zero;
		_visual.Visible = false;
		_hazardArea.Monitoring = false;
		_hazardArea.Monitorable = false;

		if (ExplosionScene is not null)
		{
			Node explosionNode = ExplosionScene.Instantiate();
			if (explosionNode is Explosion explosion)
				explosion.TargetScale = ExplosionScale;

			GetTree().CurrentScene.AddChild(explosionNode);
			((Node2D)explosionNode).GlobalPosition = GlobalPosition;
		}

		GetTree().CreateTimer(RespawnDelay).Timeout += Respawn;
	}

	private void Respawn()
	{
		Position = _spawnPosition;
		Velocity = Vector2.Zero;
		_airTime = 0f;
		_visual.Visible = true;
		_hazardArea.Monitoring = true;
		_hazardArea.Monitorable = true;
		_exploding = false;
	}
}
