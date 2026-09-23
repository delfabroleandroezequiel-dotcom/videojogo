using System.Collections.Generic;
using Godot;

namespace Metroidvania.World;

// Procedural weapon-slash streak for enemies: same Line2D glow+core+sparks recipe as the player's
// combo trails (Player.RunSwordArcTrail / RunSwordCrescentTrail), traced as an arc around a center
// point. Only draws — each enemy decides its own arc (angles/radius/timing) per attack and calls
// Play(). Add it under the enemy's Visual node so Visual's facing flip mirrors the arc for free.
// Angles: 0° = front, -90° = straight up, +90° = straight down. Additive blend is what makes the
// colors read as "neon" instead of flat paint.
public partial class EnemySlashTrail : Node2D
{
	public float CoreWidth = 3f;
	public float GlowWidth = 9f;
	public Color GlowColor = new(1f, 0.05f, 0.12f, 1f);
	public Color CoreColor = new(1f, 0.55f, 0.6f, 1f);
	public float MinPointSpacing = 1.5f;
	// Optional look overrides (null = the default neon look). GlowRamp replaces the glow line's
	// plain transparent->opaque fade with a color ramp along the streak (tail -> tip) — e.g. a
	// fire trail cooling from deep red at the tail to orange at the tip; pair it with a white
	// GlowColor so the ramp's own colors show unmodified. SparkProcess swaps the lightning sparks
	// for another particle material (e.g. FlameEmberProcess).
	public Gradient GlowRamp;
	public ParticleProcessMaterial SparkProcess;
	public Color? SparkColor;
	public int SparkAmount = 16;
	public double SparkLifetime = 0.35;

	private Line2D _glow;
	private Line2D _core;
	private GpuParticles2D _sparks;
	private readonly List<Vector2> _points = new();
	private int _runId;

	public override void _Ready()
	{
		var additive = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
		var fade = new Gradient();
		fade.SetColor(0, new Color(1f, 1f, 1f, 0f));
		fade.SetColor(1, new Color(1f, 1f, 1f, 1f));
		var taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0.2f));
		taper.AddPoint(new Vector2(1f, 1f));

		Line2D MakeLine(float width) => new()
		{
			Material = additive,
			Width = width,
			WidthCurve = taper,
			Gradient = fade,
			BeginCapMode = Line2D.LineCapMode.Round,
			EndCapMode = Line2D.LineCapMode.Round,
		};

		_glow = MakeLine(GlowWidth);
		if (GlowRamp is not null)
			_glow.Gradient = GlowRamp;
		_core = MakeLine(CoreWidth);
		_sparks = new GpuParticles2D
		{
			Material = additive,
			Emitting = false,
			Amount = SparkAmount,
			Lifetime = SparkLifetime,
			Randomness = 0.6f,
			ProcessMaterial = SparkProcess ?? GD.Load<ParticleProcessMaterial>("res://resources/particles/LightningSparkProcess.tres"),
			Texture = GD.Load<Texture2D>("res://resources/particles/SoftDotGradient.tres"),
			Scale = new Vector2(0.5f, 0.5f),
			Modulate = SparkColor ?? GlowColor,
		};
		AddChild(_glow);
		AddChild(_core);
		AddChild(_sparks);
	}

	public async void Play(Vector2 center, float radius, float startAngle, float endAngle, float duration)
	{
		int runId = ++_runId;
		_points.Clear();
		_glow.Points = System.Array.Empty<Vector2>();
		_core.Points = System.Array.Empty<Vector2>();
		_glow.Modulate = GlowColor;
		_core.Modulate = CoreColor;
		_sparks.Emitting = true;

		float elapsed = 0f;
		while (elapsed < duration)
		{
			float t = Mathf.Clamp(elapsed / duration, 0f, 1f);
			float angleRad = Mathf.DegToRad(Mathf.Lerp(startAngle, endAngle, t));
			Vector2 tip = center + Vector2.Right.Rotated(angleRad) * radius;
			if (_points.Count == 0 || _points[^1].DistanceSquaredTo(tip) > MinPointSpacing * MinPointSpacing)
			{
				_points.Add(tip);
				Vector2[] points = _points.ToArray();
				_glow.Points = points;
				_core.Points = points;
			}
			_sparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _runId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		_sparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_glow, "modulate:a", 0f, 0.1f);
		fadeTween.Parallel().TweenProperty(_core, "modulate:a", 0f, 0.1f);
	}

	// Straight thrust streak (a lunge, not a swing — same idea as Player.RunSwordThrustTrail):
	// grows from rom to 	o over extendDuration, holds holdDuration, then fades.
	public async void PlayThrust(Vector2 from, Vector2 to, float extendDuration, float holdDuration)
	{
		int runId = ++_runId;
		_glow.Modulate = GlowColor;
		_core.Modulate = CoreColor;
		_sparks.Emitting = false;

		float elapsed = 0f;
		while (elapsed < extendDuration)
		{
			float t = Mathf.Clamp(elapsed / Mathf.Max(0.001f, extendDuration), 0f, 1f);
			Vector2 tip = from.Lerp(to, t);
			Vector2[] points = { from, tip };
			_glow.Points = points;
			_core.Points = points;
			_sparks.Position = tip;

			await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
			if (!IsInstanceValid(this) || runId != _runId)
				return;
			elapsed += (float)GetPhysicsProcessDeltaTime();
		}

		Vector2[] full = { from, to };
		_glow.Points = full;
		_core.Points = full;
		_sparks.Position = to;
		_sparks.Emitting = true;

		await ToSignal(GetTree().CreateTimer(holdDuration), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || runId != _runId)
			return;

		_sparks.Emitting = false;
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(_glow, "modulate:a", 0f, 0.1f);
		fadeTween.Parallel().TweenProperty(_core, "modulate:a", 0f, 0.1f);
	}
}