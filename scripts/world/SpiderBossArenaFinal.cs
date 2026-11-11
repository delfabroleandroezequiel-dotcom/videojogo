using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The end of the arena spider (user, 2026-11-10): a death worth the fight, and Gransin closing it.
//  - The killing blow: white flash, slow motion, the long death shriek (SpiderBossArenaFx) while it
//    rolls belly-up (its "death" clip); a couple of leg twitches after; dust and grit sift down
//    from the ceiling. RockslideBlocker blows the pile open on its own (Died).
//  - Then Gransin, from behind where the rocks were: "Is it... over? Here! I'm in here!"
// Also the music for the last phase: when it tears free the theme drops away for the quake and
// comes back a touch faster and louder (EntradaArana owns the music player).
public partial class SpiderBossArena
{
	[ExportGroup("Final")]
	[Export] public NodePath EntradaPath = "../EntradaArana";
	[Export] public string GransinLineKey = "CIN_ARANA_GRANSIN_2";
	[Export] public float GransinLineDelay = 3.4f;
	// Where his voice comes from: the pile (his room is behind it).
	[Export] public NodePath GransinVoiceFrom = "../Pedrasboss";
	[Export] public float FinalPhaseMusicPitch = 1.06f;

	private async void DeathSequence()
	{
		// Read now: RockslideBlocker frees the pile on this same death, so by the time Gransin
		// speaks it's gone (and falling back to the spider put his voice inside its corpse).
		Node2D pile = GetNodeOrNull<Node2D>(GransinVoiceFrom);
		Vector2 gransinAt = IsInstanceValid(pile) ? pile.GlobalPosition : GlobalPosition + new Vector2(300f, -120f);

		ScreenFlash(0.6f, 0.45f);
		SlowMotion(0.25f, 0.75f);
		TrapKit.Shake(this, 12f, 1f);
		TrapKit.Burst(this, GlobalPosition + new Vector2(0f, -50f), new Color(0.45f, 0.08f, 0.1f, 1f), 50, 340f, 0.7f, 0.09f);

		// Dust sifting down over the arena while it dies.
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < 16; i++)
		{
			float x = rng.RandfRange(ArenaMinX, ArenaMaxX + 250f);
			DropGrit(new Vector2(x, ViewTopY() + rng.RandfRange(-10f, 30f)), GroundY, rng.RandfRange(1.5f, 3.5f));
			await ToSignal(GetTree().CreateTimer(0.09f, false), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this))
				return;
		}

		// Two last twitches of the legs once it lies still.
		for (int i = 0; i < 2; i++)
		{
			await ToSignal(GetTree().CreateTimer(0.55f + i * 0.4f, false), SceneTreeTimer.SignalName.Timeout);
			if (!IsInstanceValid(this) || Sprite is null)
				return;
			Tween twitch = CreateTween();
			twitch.TweenProperty(Sprite, "rotation", 0.035f * (i == 0 ? 1f : -1f), 0.05f);
			twitch.TweenProperty(Sprite, "rotation", 0f, 0.18f).SetTrans(Tween.TransitionType.Elastic);
			PlaySound("Patas");
		}

		await ToSignal(GetTree().CreateTimer(Mathf.Max(0f, GransinLineDelay - 1.95f), false), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || string.IsNullOrEmpty(GransinLineKey))
			return;
		// From where the pile stood (his room is behind it).
		var voice = new Node2D();
		GetTree().CurrentScene.AddChild(voice);
		voice.GlobalPosition = gransinAt;
		AudioStream stream = Sfx.LoadVoice(GransinLineKey);
		float length = stream is null ? 3f : (float)stream.GetLength();
		Sfx.PlayVoice(this, GransinLineKey, voice);
		FloatingSubtitle.Show(voice, GransinLineKey, length + 0.6f, 40f);
		GetTree().CreateTimer(length + 2f, false).Timeout += () => { if (IsInstanceValid(voice)) voice.QueueFree(); };
	}

	// Called when it tears free: the theme falls silent for the quake and returns heavier.
	private void IntensifyMusic(float silence)
	{
		if (GetNodeOrNull<EntradaArana>(EntradaPath) is { } entrada)
			entrada.IntensificarMusica(silence, FinalPhaseMusicPitch);
	}
}
