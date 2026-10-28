using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// A SecretWall only the charged draw can break: same breakable-wall mechanism (Stats child, break
// on 0 HP, SaveManager persistence, optional overlay layer), but damage never accumulates — any
// hit that doesn't take it to 0 in one blow is shrugged off and the wall heals back to full. With
// the default MaxHealth 30, the base sword (15) and every combo/thrust pulse bounce off while the
// charged draw (15 × 2.5 ≈ 38) goes through. Raise MaxHealth along with the player's AttackPower
// upgrades if normal hits ever reach it.
// Unlike a plain SecretWall it's meant to be seen: glowing red cracks (the same red as the
// player's "charge ready" blink) tell the player what it wants; a weak hit flashes them, shakes
// the wall and clinks.
[Tool]
public partial class ChargedWall : SecretWall
{
	[Export] public Color CrackColor = new(1f, 0.25f, 0.25f, 1f);
	[Export] public string BlockedSoundCategory = "Combat/Sword";
	[Export] public string BlockedSoundKey = "Sword Blocked";

	private Stats _stats;
	private float _flash;
	private float _shake;
	private Vector2 _drawnSize;

	public override void _Ready()
	{
		base._Ready();
		QueueRedraw();
		if (Engine.IsEditorHint() || IsQueuedForDeletion())
			return;
		_stats = GetNode<Stats>("Stats");
		_stats.HitTaken += OnHitTaken;
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
		{
			if (_drawnSize != new Vector2(Width, Height))
				QueueRedraw();
			return;
		}
		if (_flash <= 0f && _shake <= 0f)
			return;
		float dt = (float)delta;
		_flash = Mathf.Max(0f, _flash - dt * 4f);
		_shake = Mathf.Max(0f, _shake - dt);
		QueueRedraw();
	}

	private void OnHitTaken(bool isProjectile)
	{
		// The lethal blow runs Died right after this and breaks the wall as usual.
		if (_stats.CurrentHealth <= 0)
			return;
		_stats.ResetToFull();
		_flash = 1f;
		_shake = 0.18f;
		QueueRedraw();
		if (!string.IsNullOrEmpty(BlockedSoundKey))
			Sfx.PlayAt(this, BlockedSoundCategory, BlockedSoundKey);
	}

	public override void _Draw()
	{
		_drawnSize = new Vector2(Width, Height);
		Vector2 half = _drawnSize / 2f;
		Vector2 offset = _shake > 0f ? new Vector2(Mathf.Sin(_shake * 140f) * 3f, 0f) : Vector2.Zero;

		Color glow = CrackColor with { A = 0.25f + 0.5f * _flash };
		Color core = CrackColor.Lerp(Colors.White, 0.6f * _flash);

		// Outline, so the wall reads as a distinct block even with ShowFill off.
		Rect2 rect = new(-half + offset, _drawnSize);
		DrawRect(rect, CrackColor with { A = 0.35f + 0.4f * _flash }, false, 2f);

		// A fixed jagged crack pattern scaled to the wall: one trunk down the middle and a few
		// branches, so it always looks the same for the same size.
		Vector2 P(float x, float y) => new Vector2(x * half.X, y * half.Y) + offset;
		Vector2[][] cracks =
		{
			new[] { P(0.05f, -0.95f), P(-0.15f, -0.6f), P(0.12f, -0.3f), P(-0.1f, 0.05f), P(0.15f, 0.4f), P(-0.05f, 0.7f), P(0.1f, 0.95f) },
			new[] { P(0.12f, -0.3f), P(0.55f, -0.42f), P(0.8f, -0.2f) },
			new[] { P(-0.1f, 0.05f), P(-0.5f, 0.15f), P(-0.75f, -0.05f) },
			new[] { P(0.15f, 0.4f), P(0.5f, 0.55f), P(0.7f, 0.8f) },
			new[] { P(-0.15f, -0.6f), P(-0.55f, -0.7f) },
		};
		foreach (Vector2[] crack in cracks)
		{
			DrawPolyline(crack, glow, 6f);
			DrawPolyline(crack, core, 2f);
		}
	}
}
