using System.Threading.Tasks;
using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The crimson spider's entrance in SpiderBossArena. Holds the beats the generic cinematic steps
// can't do, called with StepCall: Tirar (it lunges and the rockslide yanks it back,
// DerrumbeForcejeo.Tug) and Despertar (name on screen, boss music, the fight starts:
// SpiderBossArena.Awaken). Sits on the spider's face so the cinematic's close-up can aim at it.
//
// Once the cinematic has been seen, coming back (after dying) plays a short version without
// taking control: tug, awake.
[GlobalClass]
public partial class EntradaArana : Node2D
{
	[Export] public NodePath BossPath = "../SpiderBossArena";
	[Export] public NodePath ForcejeoPath = "../Pedrasboss/Forcejeo";
	// The cinematic's PersistenceId; when it's already been seen, the short version plays instead.
	[Export] public string CinematicId = "cine_arana_entrada";
	[Export] public string BossNameKey = "BOSS_ARANA_NOMBRE";
	[Export] public AudioStream BossMusic;
	[Export] public float MusicVolumeDb = -6f;

	private Node2D _boss;
	private MusicCrossfader _music;
	private bool _awake;

	public override void _Ready()
	{
		_boss = GetNodeOrNull<Node2D>(BossPath);
		// Already beaten: there's no entrance to play (and Gransin is long gone from behind the rocks).
		// This node sits before the cinematic, so marking it seen here stops it from starting.
		if (_boss is null || _boss.IsQueuedForDeletion())
		{
			if (!string.IsNullOrEmpty(CinematicId))
				SaveManager.Instance.MarkCutscenePlayed(CinematicId);
			return;
		}
		if (!string.IsNullOrEmpty(CinematicId) && SaveManager.Instance.IsCutscenePlayed(CinematicId))
			_ = ShortVersion();
	}

	private async Task ShortVersion()
	{
		await ToSignal(GetTree().CreateTimer(1.4f), SceneTreeTimer.SignalName.Timeout);
		if (!IsInsideTree())
			return;
		Tirar();
		// Tirar first plays the ~0.9 s wake-up, then tugs: wake the fight just after that.
		await ToSignal(GetTree().CreateTimer(1.3f), SceneTreeTimer.SignalName.Timeout);
		if (IsInsideTree())
			Despertar(showName: false);
	}

	// She's asleep until here: first she pushes herself up (SpiderBossArena.WakeUp), then lunges
	// and the rockslide yanks her back.
	public async void Tirar()
	{
		if (_boss is SpiderBossArena spider)
		{
			spider.WakeUp();
			while (spider.Waking && IsInstanceValid(spider) && IsInsideTree())
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		if (IsInsideTree() && GetNodeOrNull<DerrumbeForcejeo>(ForcejeoPath) is { } forcejeo)
			forcejeo.Tug();
	}

	public void Despertar() => Despertar(showName: true);

	// The spider's last phase (SpiderBossArena tearing free): the theme drops almost to nothing for
	// the quake, then comes back on the landing a touch faster/higher and louder.
	public async void IntensificarMusica(float silenceSeconds, float pitch)
	{
		if (_music is null || !IsInstanceValid(_music))
			return;
		_music.CreateTween().TweenProperty(_music, "FadeGain", 0.12f, 0.35f);
		await ToSignal(GetTree().CreateTimer(silenceSeconds, false), SceneTreeTimer.SignalName.Timeout);
		if (_music is null || !IsInstanceValid(_music))
			return;
		_music.PitchScale = pitch;
		_music.VolumeDb += 2f;
		_music.CreateTween().TweenProperty(_music, "FadeGain", 1f, 0.2f);
	}

	private void Despertar(bool showName)
	{
		if (_awake)
			return;
		_awake = true;
		if (showName && !string.IsNullOrEmpty(BossNameKey))
			Metroidvania.UI.ZoneTitle.Instance?.Show(TranslationServer.Translate(BossNameKey));
		if (BossMusic is not null)
		{
			_music = new MusicCrossfader { Name = "MusicaJefe", VolumeDb = MusicVolumeDb };
			AddChild(_music);
			_music.Play(BossMusic, 0.4f);
			if (_boss is Enemy enemy && enemy.Stats is not null)
				enemy.Stats.Died += () => _music?.Stop(3f);
		}
		if (_boss is SpiderBossArena spider)
			spider.Awaken();
	}
}
