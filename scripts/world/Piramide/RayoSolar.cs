using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Rayo de sol (La Pirámide): a shaft of sunlight that enters through a slit at the node and travels
// along Direction. It bounces off every EspejoSolar it meets (group "espejo_solar"), stops on solid
// collision (WallMask, so doors and walls block it) and lights any ReceptorSolar it touches (group
// "receptor_solar"; a receptor absorbs the beam). It also burns: touching the beam hurts the player
// and shoves them out of it, so the room's mirrors are at once the puzzle and the hazard — turning a
// mirror to open a door can sweep the beam across the path you are standing on.
//
// Modes: Siempre (always on) · Ciclo (OffTime dark → WarningTime faint flicker that doesn't hurt →
// OnTime lit; read the pattern and cross in the dark) · Manual (Activate/Deactivate/Toggle from a
// PressurePlate, TrapRelay or Lever; StartsOn sets the initial state).
public partial class RayoSolar : Node2D
{
	public enum Modo { Siempre, Ciclo, Manual }

	[Export] public Modo Mode = Modo.Siempre;
	[Export] public Vector2 Direction = Vector2.Down;
	[Export] public float MaxLength = 4000f;
	[Export] public int MaxBounces = 12;
	[Export(PropertyHint.Layers2DPhysics)] public uint WallMask = 1;
	[Export] public bool StartsOn = true;
	[Export] public float OnTime = 2.4f;
	[Export] public float OffTime = 2.0f;
	[Export] public float WarningTime = 0.6f;
	[Export] public float StartOffset;
	[Export] public bool Hurts = true;
	[Export] public int Damage = 20;
	[Export] public float KnockbackForce = 340f;
	[Export] public float RehitDelay = 0.7f;
	[Export] public float BeamWidth = 10f;
	[Export] public Color BeamColor = new(1f, 0.8f, 0.38f, 1f);

	private readonly List<Vector2> _points = new();
	private bool _manualOn;
	private float _t;
	private float _rehit;
	private float _time;
	private PointLight2D _sourceLight;
	private PointLight2D _endLight;
	private CpuParticles2D _endSparks;

	// 0 = dark, 1 = faint (warning, harmless), 2 = burning.
	public int State { get; private set; }
	public IReadOnlyList<Vector2> Points => _points;

	public override void _Ready()
	{
		Material = TrapKit.Additive();
		ZIndex = 4;
		_manualOn = StartsOn;
		_t = StartOffset;
		_sourceLight = TrapKit.Light(BeamColor, 1.4f, 1.2f);
		AddChild(_sourceLight);
		_endLight = TrapKit.Light(BeamColor, 1.1f, 0.8f);
		AddChild(_endLight);
		_endSparks = new CpuParticles2D
		{
			Emitting = false,
			Amount = 18,
			Lifetime = 0.45f,
			Spread = 180f,
			Gravity = new Vector2(0f, 300f),
			InitialVelocityMin = 40f,
			InitialVelocityMax = 120f,
			ScaleAmountMin = 1.5f,
			ScaleAmountMax = 3f,
			Color = new Color(BeamColor, 0.9f),
		};
		AddChild(_endSparks);
	}

	public void Activate() => _manualOn = true;
	public void Deactivate() => _manualOn = false;
	public void Toggle() => _manualOn = !_manualOn;

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_time += dt;
		_rehit = Mathf.Max(0f, _rehit - dt);
		State = ComputeState(dt);
		Trace();

		bool lit = State > 0;
		_sourceLight.Visible = lit;
		_endLight.Visible = lit;
		_sourceLight.Energy = State == 2 ? 1.4f : 0.35f;
		_endLight.Energy = State == 2 ? 1.1f : 0.25f;
		_endSparks.Emitting = State == 2;
		if (_points.Count > 1)
		{
			_endLight.Position = ToLocal(_points[^1]);
			_endSparks.Position = _endLight.Position;
		}

		if (State == 2 && Hurts && _rehit <= 0f)
			TryBurnPlayer();
		QueueRedraw();
	}

	private int ComputeState(float dt)
	{
		switch (Mode)
		{
			case Modo.Siempre:
				return 2;
			case Modo.Manual:
				return _manualOn ? 2 : 0;
			default:
				float total = OffTime + WarningTime + OnTime;
				_t = (_t + dt) % total;
				if (_t < OffTime)
					return 0;
				return _t < OffTime + WarningTime ? 1 : 2;
		}
	}

	// Walks the beam: wall raycast per leg, then the nearest mirror or receptor closer than the wall.
	private void Trace()
	{
		_points.Clear();
		Vector2 origin = GlobalPosition;
		Vector2 dir = Direction == Vector2.Zero ? Vector2.Down : Direction.Normalized();
		float remaining = MaxLength;
		_points.Add(origin);
		EspejoSolar lastMirror = null;
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;

		for (int bounce = 0; bounce <= MaxBounces && remaining > 1f; bounce++)
		{
			float wallT = remaining;
			var query = PhysicsRayQueryParameters2D.Create(origin + dir * 1.5f, origin + dir * remaining, WallMask);
			query.CollideWithAreas = false;
			var hit = space.IntersectRay(query);
			if (hit.Count > 0)
				wallT = origin.DistanceTo((Vector2)hit["position"]);

			EspejoSolar bestMirror = null;
			float bestT = wallT;
			foreach (Node n in GetTree().GetNodesInGroup("espejo_solar"))
			{
				if (n is not EspejoSolar m || m == lastMirror)
					continue;
				if (m.Intersect(origin, dir, out float t) && t < bestT && t > 0.5f)
				{
					bestT = t;
					bestMirror = m;
				}
			}

			ReceptorSolar bestReceptor = null;
			foreach (Node n in GetTree().GetNodesInGroup("receptor_solar"))
			{
				if (n is not ReceptorSolar r)
					continue;
				if (r.Intersect(origin, dir, out float t) && t < bestT && t > 0.5f)
				{
					bestT = t;
					bestReceptor = r;
					bestMirror = null;
				}
			}

			Vector2 end = origin + dir * bestT;
			_points.Add(end);
			remaining -= bestT;

			if (bestReceptor is not null)
			{
				if (State == 2)
					bestReceptor.Iluminar();
				return;
			}
			if (bestMirror is null)
				return;
			dir = bestMirror.Reflect(dir);
			origin = end;
			lastMirror = bestMirror;
		}
	}

	private void TryBurnPlayer()
	{
		if (TrapKit.FindPlayer(this) is not Node2D player)
			return;
		Vector2 c = player.GlobalPosition;
		float halfW = 14f + BeamWidth / 2f;
		float halfH = 34f + BeamWidth / 2f;
		for (int i = 0; i < _points.Count - 1; i++)
		{
			Vector2 a = _points[i];
			Vector2 b = _points[i + 1];
			Vector2 closest = Geometry2D.GetClosestPointToSegment(c, a, b);
			Vector2 off = c - closest;
			if (Mathf.Abs(off.X) > halfW || Mathf.Abs(off.Y) > halfH)
				continue;
			// Shove perpendicular to the leg, away from the beam (up if it's a horizontal floor beam).
			Vector2 leg = (b - a).Normalized();
			Vector2 normal = new(-leg.Y, leg.X);
			if (normal.Dot(off) < 0f)
				normal = -normal;
			if (Mathf.Abs(leg.X) > 0.9f)
				normal = new Vector2(0.35f * Mathf.Sign(off.X == 0f ? 1f : off.X), -1f).Normalized();
			if (TrapKit.Hurt(player, Damage, normal, KnockbackForce))
			{
				_rehit = RehitDelay;
				TrapKit.Burst(this, closest, BeamColor, 16, 160f);
			}
			return;
		}
	}

	public override void _Draw()
	{
		if (State == 0 || _points.Count < 2)
		{
			// The slit itself, so a dark beam's source still reads.
			DrawCircle(Vector2.Zero, 6f, new Color(BeamColor, 0.25f));
			return;
		}
		var local = new Vector2[_points.Count];
		for (int i = 0; i < _points.Count; i++)
			local[i] = ToLocal(_points[i]);
		float flicker = 0.85f + 0.15f * Mathf.Sin(_time * 23f);
		if (State == 1)
		{
			float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 30f);
			DrawPolyline(local, new Color(BeamColor, 0.12f + 0.18f * pulse), BeamWidth * 0.5f);
			return;
		}
		DrawPolyline(local, new Color(BeamColor, 0.16f * flicker), BeamWidth * 3.2f);
		DrawPolyline(local, new Color(BeamColor, 0.45f * flicker), BeamWidth * 1.4f);
		// HDR core: >1 so the post-process glow catches it (flare, not a flat line).
		DrawPolyline(local, new Color(1.9f, 1.7f, 1.25f, 1f), BeamWidth * 0.45f);
		DrawCircle(local[0], BeamWidth, new Color(BeamColor, 0.6f));
		DrawCircle(local[^1], BeamWidth * 0.9f, new Color(1.6f, 1.4f, 1f, 0.8f * flicker));
	}
}
