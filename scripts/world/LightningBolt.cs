using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Homemade lightning bolt, redesigned after studying assets/external/ThunderEffectPack's
// "Effect 10" sprite sheet (a 128x400 vertical strike): that pack does NOT redraw a new random
// path every frame — it draws ONE bold, few-segment bolt and animates its breakup/fade, with a
// handful of short branches clustered near the impact end rather than one long diagonal fork.
// Redrawing a fresh random path every flicker (the old approach here) is exactly what read as
// "random" instead of a strike: the path is now built ONCE in Strike() and held fixed; only its
// width/brightness decay and a few crackle sparks along its length animate afterwards.
public partial class LightningBolt : Node2D
{
	// Few, bold segments (like the reference's angular zigzag) rather than many small wobbles.
	[Export] public int Iterations = 3;
	// Fraction of the from->to distance used as the first level of jitter; each further
	// subdivision shrinks it by JitterFalloff. Proportional instead of a fixed pixel value so a
	// short test bolt and a full screen-height boss strike both zigzag by the same relative
	// amount instead of the short one looking chaotic and the long one looking dead straight.
	[Export] public float JitterRatio = 0.11f;
	[Export] public float JitterFalloff = 0.55f;
	[Export] public float FlickerInterval = 0.05f;
	[Export] public float Duration = 0.5f;
	[Export(PropertyHint.Range, "0,1")] public float BranchChance = 0.75f;
	[Export] public float BranchLengthFactor = 0.22f;
	// Along-bolt crackle bursts (not just the impact point) fire only in this trailing fraction
	// of Duration, so the strike reads solid before it starts visibly breaking apart.
	[Export(PropertyHint.Range, "0,1")] public float CrackleStartFraction = 0.35f;
	// How long the very first flash takes to "resolve" from one homogeneous bright mass into
	// the normal separated core+halo look (see RunDecay).
	[Export] public float ResolveDuration = 0.12f;
	// Width multipliers along the trunk's own width_curve: thin near the origin, flared at the
	// impact end, so the impact always reads as the wider point instead of only when a random
	// branch happens to spawn there.
	[Export] public float OriginWidthTaper = 0.75f;
	[Export] public float ImpactWidthFlare = 1.6f;

	private Line2D _glow;
	private Line2D _core;
	private Line2D _secondary;
	private Line2D _branchGlow;
	private Line2D _branchCore;
	private GpuParticles2D _sparks;
	private GpuParticles2D _crackle;
	private PointLight2D _flash;
	private readonly RandomNumberGenerator _rng = new();
	private Vector2[] _points;
	private float _baseGlowWidth;
	private float _baseCoreWidth;
	private float _baseBranchGlowWidth;
	private float _baseBranchCoreWidth;
	private float _baseFlashEnergy;

	public override void _Ready()
	{
		_rng.Randomize();
		_glow = GetNode<Line2D>("Glow");
		_core = GetNode<Line2D>("Core");
		_secondary = GetNode<Line2D>("Secondary");
		_branchGlow = GetNode<Line2D>("BranchGlow");
		_branchCore = GetNode<Line2D>("BranchCore");
		_sparks = GetNode<GpuParticles2D>("Sparks");
		_crackle = GetNode<GpuParticles2D>("Crackle");
		_flash = GetNode<PointLight2D>("Flash");

		_baseGlowWidth = _glow.Width;
		_baseCoreWidth = _core.Width;
		_baseBranchGlowWidth = _branchGlow.Width;
		_baseBranchCoreWidth = _branchCore.Width;
		_baseFlashEnergy = _flash.Energy;

		// Consistent taper along the trunk itself (thin at the origin, flared at the impact
		// end) instead of relying on the random branch to be what makes the bottom look wider —
		// that read as inconsistent since branches only spawn some of the time.
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, OriginWidthTaper));
		taper.AddPoint(new Vector2(1f, ImpactWidthFlare));
		_glow.WidthCurve = taper;
		_core.WidthCurve = taper;
	}

	// from/to are global positions. Call right after instancing.
	public void Strike(Vector2 from, Vector2 to)
	{
		Vector2 from2 = ToLocal(from);
		Vector2 to2 = ToLocal(to);
		float jitter = (to2 - from2).Length() * JitterRatio;

		_points = BuildJaggedPath(from2, to2, jitter, JitterFalloff, Iterations);
		_glow.Points = _points;
		_core.Points = _points;
		_secondary.Points = BuildJaggedPath(from2, to2, jitter * 0.6f, JitterFalloff, Iterations);

		SpawnBranches(_points, from2, to2, jitter);

		_sparks.GlobalPosition = to;
		_sparks.Restart();
		_sparks.Emitting = true;
		_flash.GlobalPosition = to;

		RunDecay();
	}

	// Short forks clustered in the last stretch before impact (like the reference's branching
	// only near the bottom) instead of one long diagonal fork off the middle of the bolt.
	private void SpawnBranches(Vector2[] points, Vector2 from, Vector2 to, float jitter)
	{
		bool hasBranch = points.Length > 3 && _rng.Randf() < BranchChance;
		_branchGlow.Visible = hasBranch;
		_branchCore.Visible = hasBranch;
		if (!hasBranch)
			return;

		int lastThirdStart = Mathf.Max(1, points.Length - Mathf.Max(2, points.Length / 3));
		int startIndex = _rng.RandiRange(lastThirdStart, points.Length - 2);
		Vector2 branchStart = points[startIndex];
		float mainAngle = (to - from).Angle();
		float sign = _rng.Randf() < 0.5f ? -1f : 1f;
		float angle = mainAngle + _rng.RandfRange(0.5f, 1.2f) * sign;
		Vector2 branchEnd = branchStart + Vector2.Right.Rotated(angle) * (to - from).Length() * BranchLengthFactor;

		Vector2[] branchPoints = BuildJaggedPath(branchStart, branchEnd, jitter * 0.7f, JitterFalloff, Mathf.Max(1, Iterations - 1));
		_branchGlow.Points = branchPoints;
		_branchCore.Points = branchPoints;
	}

	private async void RunDecay()
	{
		float elapsed = 0f;
		bool first = true;
		while (elapsed < Duration)
		{
			float progress = elapsed / Duration;
			// decay stays near 1 right after the initial strike and eases toward 0 by the end —
			// width, flash and overall alpha all fade together off the SAME fixed shape, instead
			// of the shape itself changing, which is what used to read as random flicker.
			float decay = first ? 1f : Mathf.Lerp(0.6f, 0f, progress);
			float widthMul = first ? 1.7f : 0.7f + decay * 0.5f;

			// The very first instant reads as one solid, homogeneous flash rather than an
			// already-separated "thin core + soft wide halo" — the glow collapses down to
			// nearly the core's own width and washes toward white (additive blend, so an
			// over-1 modulate overexposes it) for ResolveDuration, then relaxes back out into
			// its normal wider, tinted halo as the bolt "resolves" into the detailed look.
			float resolveT = Mathf.Clamp(elapsed / ResolveDuration, 0f, 1f);
			float flashBoost = 1f - resolveT;
			float collapsedGlowWidthMul = (_baseCoreWidth * 1.3f) / _baseGlowWidth;
			float glowWidthMul = Mathf.Lerp(widthMul, collapsedGlowWidthMul, flashBoost);

			_glow.Width = _baseGlowWidth * glowWidthMul;
			_core.Width = _baseCoreWidth * widthMul;
			_glow.Modulate = new Color(1f + flashBoost * 0.6f, 1f + flashBoost * 0.6f, 1f + flashBoost * 0.6f, 1f);
			// Branches were staying at their full authored width while the trunk thinned out
			// during decay, which is what made the impact end look disproportionately wide/bulky.
			if (_branchGlow.Visible)
			{
				_branchGlow.Width = _baseBranchGlowWidth * widthMul;
				_branchCore.Width = _baseBranchCoreWidth * widthMul;
			}
			_flash.Energy = first ? _baseFlashEnergy * 1.7f : _baseFlashEnergy * (0.25f + decay * 0.75f);
			Modulate = new Color(1f, 1f, 1f, first ? 1f : 0.5f + decay * 0.5f);

			if (!first && progress >= CrackleStartFraction && _points.Length > 2 && _rng.Randf() < 0.55f)
			{
				Vector2 point = _points[_rng.RandiRange(1, _points.Length - 2)];
				_crackle.GlobalPosition = ToGlobal(point);
				_crackle.Restart();
				_crackle.Emitting = true;
			}

			first = false;
			await ToSignal(GetTree().CreateTimer(FlickerInterval), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
			elapsed += FlickerInterval;
		}

		QueueFree();
	}

	private Vector2[] BuildJaggedPath(Vector2 from, Vector2 to, float jitter, float falloff, int iterations)
	{
		var points = new List<Vector2> { from, to };
		for (int i = 0; i < iterations; i++)
		{
			var next = new List<Vector2> { points[0] };
			for (int j = 0; j < points.Count - 1; j++)
			{
				Vector2 a = points[j];
				Vector2 b = points[j + 1];
				Vector2 mid = (a + b) * 0.5f;
				Vector2 dir = (b - a).Normalized();
				Vector2 normal = new Vector2(-dir.Y, dir.X);
				mid += normal * _rng.RandfRange(-1f, 1f) * jitter;
				next.Add(mid);
				next.Add(b);
			}
			points = next;
			jitter *= falloff;
		}
		return points.ToArray();
	}

	public static LightningBolt SpawnStrike(Node context, Vector2 from, Vector2 to)
	{
		var scene = GD.Load<PackedScene>("res://scenes/world/Rehusables/LightningBolt.tscn");
		var bolt = scene.Instantiate<LightningBolt>();
		context.GetTree().CurrentScene.AddChild(bolt);
		bolt.Strike(from, to);
		return bolt;
	}
}
