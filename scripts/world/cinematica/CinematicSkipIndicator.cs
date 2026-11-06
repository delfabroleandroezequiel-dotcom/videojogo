using Godot;

namespace Metroidvania.World;

// The "hold to skip" corner of a Cinematic: a small ring that fills while the skip button is held,
// with the hint next to it. Invisible until the player starts holding; fades away when released.
// Built and owned by the Cinematic (not authored in a scene).
public partial class CinematicSkipIndicator : CanvasLayer
{
	public const string HintKey = "CIN_SALTEAR";

	// 0..1: how far the hold has gone.
	public float Progress
	{
		get => _ring.Progress;
		set
		{
			_ring.Progress = Mathf.Clamp(value, 0f, 1f);
			_ring.QueueRedraw();
		}
	}

	private Ring _ring;
	private Label _label;
	private Control _root;

	public override void _Ready()
	{
		Layer = 100;
		Name = "SaltearCinematica";
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
		_root.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		_root.Position = new Vector2(-240f, -64f);
		AddChild(_root);
		_ring = new Ring { Position = new Vector2(196f, 8f), Size = new Vector2(28f, 28f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(_ring);
		_label = new Label
		{
			Text = TranslationServer.Translate(HintKey),
			HorizontalAlignment = HorizontalAlignment.Right,
			Size = new Vector2(186f, 28f),
			Position = new Vector2(0f, 10f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_label.AddThemeFontSizeOverride("font_size", 14);
		_label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.8f));
		_root.AddChild(_label);
	}

	// Shown while held (or still draining back), hidden otherwise.
	public void SetShown(bool shown, float delta)
	{
		float alpha = Mathf.MoveToward(_root.Modulate.A, shown ? 1f : 0f, delta * 4f);
		_root.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	private partial class Ring : Control
	{
		public float Progress;

		public override void _Draw()
		{
			Vector2 centre = Size / 2f;
			float radius = Size.X / 2f - 2f;
			DrawArc(centre, radius, 0f, Mathf.Tau, 32, new Color(1f, 1f, 1f, 0.25f), 2.5f, true);
			if (Progress > 0f)
				DrawArc(centre, radius, -Mathf.Pi / 2f, -Mathf.Pi / 2f + Mathf.Tau * Progress, 32, Colors.White, 3f, true);
		}
	}
}
