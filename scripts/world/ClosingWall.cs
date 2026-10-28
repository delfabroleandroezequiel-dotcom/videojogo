using System.Collections.Generic;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

public enum ClosingWallTrigger
{
	// Waits for Activate() (a PressurePlate, a lever, a CutsceneTrigger...), closes once, then
	// opens back up HoldTime later and re-arms.
	Manual,
	// Closes on its own every Interval seconds — a rhythm to read (a ceiling that keeps coming down).
	Loop,
}

// A trap that closes in: a solid block with a row of neon spikes on its leading face that, once
// triggered, warns (shake + pulsing glow), slides Distance pixels along Direction and stays shut
// for HoldTime before sliding back. The spikes hurt (or kill) whatever they touch. Pointed sideways
// it's an advancing wall that chases you down a corridor; pointed down it's a ceiling that comes
// down on a room. It always reopens, so a trap the player escaped never seals the way back.
// The node's origin is the block's centre at rest; Direction is in the node's local space.
// AnimatableBody2D moved through its local Position (never rotated), so riding or being shoved by
// it stays well-behaved. Drawn in code like the rest of the neon kit; [Tool] for the editor preview
// (the block and a dashed outline where it ends up).
[Tool]
public partial class ClosingWall : AnimatableBody2D
{
	private Vector2 _size = new(64f, 160f);
	private Vector2 _direction = Vector2.Right;
	private float _distance = 400f;

	[Export]
	public Vector2 Size
	{
		get => _size;
		set { _size = new Vector2(Mathf.Max(16f, value.X), Mathf.Max(16f, value.Y)); QueueRedraw(); }
	}

	// Which way it closes. Snapped to one of the four axes; the spikes face this way.
	[Export]
	public Vector2 Direction
	{
		get => _direction;
		set { _direction = SnapAxis(value); QueueRedraw(); }
	}

	[Export]
	public float Distance
	{
		get => _distance;
		set { _distance = Mathf.Max(0f, value); QueueRedraw(); }
	}

	[Export] public ClosingWallTrigger Trigger = ClosingWallTrigger.Manual;

	[ExportGroup("Timing")]
	// Shake + glow before it moves, so the player reads what's coming.
	[Export] public float WarningTime = 0.8f;
	[Export] public float Speed = 90f;
	// Seconds it stays fully closed before opening back up.
	[Export] public float HoldTime = 2.5f;
	[Export] public float RetractSpeed = 160f;
	// Loop only: seconds between the end of one close and the start of the next.
	[Export] public float Interval = 2f;
	[Export] public float StartOffset;

	[ExportGroup("Damage")]
	[Export] public bool InstantKill = true;
	[Export] public int Damage = 40;
	[Export] public float KnockbackForce = 420f;
	[Export] public bool HurtsEnemies = true;
	// Seconds between two hits on the same body while it keeps touching the spikes.
	[Export] public float RehitDelay = 0.6f;

	[ExportGroup("Look")]
	[Export] public Color BlockColor = new(0.16f, 0.14f, 0.16f, 1f);
	[Export] public Color NeonColor = new(1f, 0.35f, 0.2f, 1f);
	[Export] public float SpikeLength = 18f;
	// Sfx category/key played when it starts moving (empty key = silent).
	[Export] public string SoundCategory = "World/Mining";
	[Export] public string SoundKey = "mine";

	private enum State { Idle, Warning, Closing, Closed, Opening }

	private State _state = State.Idle;
	private float _timer;
	private float _travelled;
	private Vector2 _restPosition;
	private Node2D _visual;
	private Line2D _glow;
	private Area2D _spikeArea;
	private readonly Dictionary<ulong, float> _rehit = new();

	public bool IsClosing => _state is State.Warning or State.Closing or State.Closed;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;
		SyncToPhysics = true;
		_restPosition = Position;
		Build();
		if (Trigger == ClosingWallTrigger.Loop)
			_timer = Mathf.Max(0f, Interval - StartOffset);
	}

	// PressurePlate/Lever entry point. Ignored while it's already closing.
	public void Activate()
	{
		if (_state != State.Idle)
			return;
		SetState(State.Warning);
	}

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
			return;
		var rect = new Rect2(-_size / 2f, _size);
		DrawRect(rect, BlockColor);
		DrawRect(rect, NeonColor, false, 2f);
		var end = new Rect2(-_size / 2f + _direction * _distance, _size);
		DrawDashedLine(end.Position, end.Position + new Vector2(end.Size.X, 0f), NeonColor with { A = 0.5f }, 2f, 8f);
		DrawDashedLine(end.Position + new Vector2(0f, end.Size.Y), end.End, NeonColor with { A = 0.5f }, 2f, 8f);
		DrawDashedLine(end.Position, end.Position + new Vector2(0f, end.Size.Y), NeonColor with { A = 0.5f }, 2f, 8f);
		DrawDashedLine(end.Position + new Vector2(end.Size.X, 0f), end.End, NeonColor with { A = 0.5f }, 2f, 8f);
		DrawLine(Vector2.Zero, _direction * _distance, NeonColor with { A = 0.6f }, 2f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;
		float dt = (float)delta;

		switch (_state)
		{
			case State.Idle:
				if (Trigger == ClosingWallTrigger.Loop)
				{
					_timer -= dt;
					if (_timer <= 0f)
						SetState(State.Warning);
				}
				break;
			case State.Warning:
				_timer -= dt;
				float k = 1f - Mathf.Clamp(_timer / Mathf.Max(0.01f, WarningTime), 0f, 1f);
				_visual.Position = new Vector2(Mathf.Sin(_timer * 70f) * (1f + 2f * k), 0f);
				_glow.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(_timer * Mathf.Lerp(10f, 40f, k)) > 0f ? 1f : 0.35f);
				if (_timer <= 0f)
					SetState(State.Closing);
				break;
			case State.Closing:
				Step(Speed * dt);
				if (_travelled >= _distance)
					SetState(State.Closed);
				break;
			case State.Closed:
				_timer -= dt;
				if (_timer <= 0f)
					SetState(State.Opening);
				break;
			case State.Opening:
				Step(-RetractSpeed * dt);
				if (_travelled <= 0f)
					SetState(State.Idle);
				break;
		}

		if (_state is State.Closing or State.Closed or State.Warning)
			HurtTouching(dt);
	}

	private void Step(float amount)
	{
		_travelled = Mathf.Clamp(_travelled + amount, 0f, _distance);
		Position = _restPosition + _direction * _travelled;
	}

	private void SetState(State state)
	{
		_state = state;
		switch (state)
		{
			case State.Idle:
				_glow.Modulate = new Color(1f, 1f, 1f, 0.45f);
				_timer = Interval;
				break;
			case State.Warning:
				_timer = WarningTime;
				break;
			case State.Closing:
				_visual.Position = Vector2.Zero;
				_glow.Modulate = Colors.White;
				_rehit.Clear();
				if (!string.IsNullOrEmpty(SoundKey))
					Sfx.PlayAt(this, SoundCategory, SoundKey);
				break;
			case State.Closed:
				_timer = HoldTime;
				break;
			case State.Opening:
				_glow.Modulate = new Color(1f, 1f, 1f, 0.45f);
				break;
		}
	}

	private void HurtTouching(float dt)
	{
		if (_rehit.Count > 0)
		{
			var ids = new List<ulong>(_rehit.Keys);
			foreach (ulong id in ids)
				_rehit[id] -= dt;
		}

		// Only the moving spikes hurt; during the warning they just glow.
		if (_state == State.Warning)
			return;

		foreach (Node2D body in _spikeArea.GetOverlappingBodies())
		{
			bool isPlayer = body.IsInGroup("player");
			if (!isPlayer && !(HurtsEnemies && body.IsInGroup("enemy")))
				continue;
			if (body.GetNodeOrNull<Stats>("Stats") is not { } stats || stats.CurrentHealth <= 0)
				continue;
			ulong id = body.GetInstanceId();
			if (_rehit.TryGetValue(id, out float wait) && wait > 0f)
				continue;
			if (stats.IsInvulnerable && !InstantKill)
				continue;
			_rehit[id] = RehitDelay;

			if (InstantKill)
			{
				stats.Kill();
				continue;
			}
			// Traps are never parried (see Hazard.TryHit).
			System.Func<bool> interceptor = stats.IncomingHitInterceptor;
			stats.IncomingHitInterceptor = null;
			try
			{
				stats.TakeDamage(Damage);
			}
			finally
			{
				stats.IncomingHitInterceptor = interceptor;
			}
			if (body.HasMethod("ApplyKnockback"))
				body.Call("ApplyKnockback", (_direction + Vector2.Up * 0.3f).Normalized(), KnockbackForce);
		}
	}

	private static Vector2 SnapAxis(Vector2 v)
	{
		if (v == Vector2.Zero)
			return Vector2.Right;
		return Mathf.Abs(v.X) >= Mathf.Abs(v.Y) ? new Vector2(Mathf.Sign(v.X), 0f) : new Vector2(0f, Mathf.Sign(v.Y));
	}

	private void Build()
	{
		CollisionLayer = 1;
		CollisionMask = 0;
		AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = _size } });

		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		Vector2 h = _size / 2f;
		_visual = new Node2D();
		AddChild(_visual);

		_visual.AddChild(new Polygon2D
		{
			Color = BlockColor,
			Polygon = new[] { new Vector2(-h.X, -h.Y), new Vector2(h.X, -h.Y), new Vector2(h.X, h.Y), new Vector2(-h.X, h.Y) },
		});
		// Horizontal bands across the block so it reads as heavy masonry, not a flat card.
		for (float y = -h.Y + 24f; y < h.Y; y += 24f)
			_visual.AddChild(new Line2D { Points = new[] { new Vector2(-h.X + 4f, y), new Vector2(h.X - 4f, y) }, Width = 1f, DefaultColor = BlockColor.Lightened(0.15f) });

		// Spikes along the leading face: a row of triangles pointing along Direction.
		Vector2 side = new(-_direction.Y, _direction.X);
		float faceHalf = Mathf.Abs(side.X) > 0.5f ? h.X : h.Y;
		float faceOffset = Mathf.Abs(_direction.X) > 0.5f ? h.X : h.Y;
		Vector2 faceCentre = _direction * faceOffset;
		var spikes = new List<Vector2>();
		const float spikeWidth = 16f;
		int count = Mathf.Max(1, Mathf.FloorToInt(faceHalf * 2f / spikeWidth));
		float step = faceHalf * 2f / count;
		var outline = new List<Vector2>();
		for (int i = 0; i < count; i++)
		{
			float a = -faceHalf + step * i;
			Vector2 p0 = faceCentre + side * a;
			Vector2 p1 = faceCentre + side * (a + step * 0.5f) + _direction * SpikeLength;
			Vector2 p2 = faceCentre + side * (a + step);
			_visual.AddChild(new Polygon2D { Color = new Color(0.55f, 0.55f, 0.6f, 1f), Polygon = new[] { p0, p1, p2 } });
			outline.Add(p0);
			outline.Add(p1);
			if (i == count - 1)
				outline.Add(p2);
		}

		_glow = new Line2D { Points = outline.ToArray(), Width = 7f, DefaultColor = NeonColor with { A = 0.35f }, Material = additive, Modulate = new Color(1f, 1f, 1f, 0.45f) };
		_visual.AddChild(_glow);
		_visual.AddChild(new Line2D { Points = outline.ToArray(), Width = 1.5f, DefaultColor = NeonColor, Material = additive });

		// Hurt strip: the spike row plus a sliver of the face, so touching the front hurts but
		// standing on top of a sideways wall or against its back doesn't.
		Vector2 stripSize = Mathf.Abs(_direction.X) > 0.5f
			? new Vector2(SpikeLength + 6f, _size.Y - 4f)
			: new Vector2(_size.X - 4f, SpikeLength + 6f);
		_spikeArea = new Area2D { CollisionLayer = 0, CollisionMask = HurtsEnemies ? 2u | 4u : 2u, Monitorable = false };
		_spikeArea.AddChild(new CollisionShape2D
		{
			Shape = new RectangleShape2D { Size = stripSize },
			Position = faceCentre + _direction * (SpikeLength * 0.5f - 3f + 3f),
		});
		AddChild(_spikeArea);
	}
}
