using Godot;

namespace Metroidvania.Player;

// Protagonist's cape, drawn outside the sprite sheets so it works on all 69 animations at once.
// A short verlet chain hangs from the shoulder (this node's position, inside Visual so it flips
// with the player); each segment is drawn as a textured quad from the PixelLab cloth strip.
// The chain lives in global space, so running, falling and turning drag it naturally.
// Lives in Visual BEFORE CharacterSprite in the tree, so it draws behind the body.
public partial class CapaProta : Node2D
{
	[Export] public Texture2D Textura;
	[Export] public NodePath SpritePath = "../CharacterSprite";
	[Export] public int Segmentos = 6;
	[Export] public float Largo = 30f;
	[Export] public float AnchoArriba = 7f;
	[Export] public float AnchoAbajo = 12f;
	[Export] public float Gravedad = 900f;
	[Export] public float Amortiguacion = 0.9f;
	// Constant push toward the back so it hangs behind the body instead of straight down.
	[Export] public float EmpujeAtras = 120f;
	// Flutter strength at full run speed (sideways noise on the lower points).
	[Export] public float Flameo = 260f;
	// How far (px) any point may swing in front of the shoulder before it is clamped back.
	[Export] public float MaxAdelante = 3f;

	private static readonly string[] Ocultar =
	{
		"roll", "swim", "climb", "ladder", "ledge", "wall_", "trepar", "death", "transformation",
	};

	private CharacterBody2D _player;
	private AnimatedSprite2D _sprite;
	private Vector2 _anclaBase;
	private Vector2[] _p;
	private Vector2[] _prev;
	private float _tiempo;
	private bool _iniciada;

	public override void _Ready()
	{
		_anclaBase = Position;
		_sprite = GetNodeOrNull<AnimatedSprite2D>(SpritePath);
		for (Node n = GetParent(); n != null; n = n.GetParent())
			if (n is CharacterBody2D body) { _player = body; break; }

		_p = new Vector2[Segmentos];
		_prev = new Vector2[Segmentos];
	}

	// Visual's own Scale.X (Player.cs sets ±1). Not GlobalScale: Godot folds a flip into scale.Y.
	private float Facing => GetParent<Node2D>().Scale.X < 0f ? -1f : 1f;

	private void Reiniciar(Vector2 ancla, float seg)
	{
		for (int i = 0; i < Segmentos; i++)
		{
			_p[i] = ancla + new Vector2(-Facing * i * 1.5f, i * seg);
			_prev[i] = _p[i];
		}
		_iniciada = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		_tiempo += dt;

		string anim = _sprite?.Animation.ToString() ?? "idle";
		bool oculta = false;
		foreach (string o in Ocultar)
			if (anim.StartsWith(o)) { oculta = true; break; }
		Visible = !oculta;
		if (oculta) { _iniciada = false; return; }

		// Crouching lowers the shoulder ~24 sprite px and the cape would drag through the floor.
		bool agachado = anim.StartsWith("crouch") || anim == "slide";
		Position = _anclaBase + (agachado ? new Vector2(0f, 26f) : Vector2.Zero);
		float largo = Largo * (agachado ? 0.55f : 1f);
		float seg = largo / (Segmentos - 1);

		Vector2 ancla = GlobalPosition;
		if (!_iniciada || ancla.DistanceTo(_p[0]) > 120f)
			Reiniciar(ancla, seg);

		float facing = Facing;
		Vector2 vel = _player?.Velocity ?? Vector2.Zero;
		float rapidez = Mathf.Clamp(Mathf.Abs(vel.X) / 250f, 0f, 1f);

		_p[0] = ancla;
		_prev[0] = ancla;
		for (int i = 1; i < Segmentos; i++)
		{
			float t = (float)i / (Segmentos - 1);
			Vector2 acc = new Vector2(-facing * EmpujeAtras * t, Gravedad);
			acc.Y += Mathf.Sin(_tiempo * 14f + i * 1.3f) * Flameo * rapidez * t;
			Vector2 actual = _p[i];
			_p[i] += (_p[i] - _prev[i]) * Amortiguacion + acc * dt * dt;
			_prev[i] = actual;
		}

		for (int it = 0; it < 4; it++)
		{
			for (int i = 1; i < Segmentos; i++)
			{
				Vector2 d = _p[i] - _p[i - 1];
				float len = d.Length();
				if (len < 0.0001f) continue;
				Vector2 corr = d * ((len - seg) / len);
				if (i == 1) _p[i] -= corr;
				else { _p[i] -= corr * 0.5f; _p[i - 1] += corr * 0.5f; }
			}
			_p[0] = ancla;
			for (int i = 1; i < Segmentos; i++)
			{
				float adelante = (_p[i].X - ancla.X) * facing;
				if (adelante > MaxAdelante)
					_p[i].X = ancla.X + MaxAdelante * facing;
			}
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Textura == null || !_iniciada)
			return;

		var local = new Vector2[Segmentos];
		for (int i = 0; i < Segmentos; i++)
			local[i] = ToLocal(_p[i]);

		var colores = new[] { Colors.White, Colors.White, Colors.White, Colors.White };
		for (int i = 0; i < Segmentos - 1; i++)
		{
			float t0 = (float)i / (Segmentos - 1);
			float t1 = (float)(i + 1) / (Segmentos - 1);
			Vector2 n0 = Normal(local, i) * Mathf.Lerp(AnchoArriba, AnchoAbajo, t0) * 0.5f;
			Vector2 n1 = Normal(local, i + 1) * Mathf.Lerp(AnchoArriba, AnchoAbajo, t1) * 0.5f;
			var quad = new[] { local[i] - n0, local[i] + n0, local[i + 1] + n1, local[i + 1] - n1 };
			var uvs = new[] { new Vector2(0f, t0), new Vector2(1f, t0), new Vector2(1f, t1), new Vector2(0f, t1) };
			// DrawPrimitive, not DrawPolygon: a quad that twists for a frame while turning would
			// fail DrawPolygon's triangulation and log an error.
			DrawPrimitive(quad, colores, uvs, Textura);
		}
	}

	private Vector2 Normal(Vector2[] pts, int i)
	{
		Vector2 dir = i == 0 ? pts[1] - pts[0]
			: i == pts.Length - 1 ? pts[i] - pts[i - 1]
			: pts[i + 1] - pts[i - 1];
		dir = dir.LengthSquared() < 0.0001f ? Vector2.Down : dir.Normalized();
		return new Vector2(-dir.Y, dir.X);
	}
}
