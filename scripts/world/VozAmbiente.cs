using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

// Background chatter that plays on its own while the player is nearby — tavern regulars talking over
// their drinks, Ostigada muttering behind the bar, bandits arguing while they loot. No dialogue box:
// each line comes from whoever says it (positional, fades with distance) with a small subtitle over
// their head.
//
// Lines play in order as one exchange (Conversation) or one at a time at random (barks). Speakers
// lists who says each line (by NodePath, same length as LineKeys); empty, or a missing entry, means
// this node itself. Speakers that get freed (a bandit killed mid-conversation) just stop talking.
[GlobalClass]
public partial class VozAmbiente : Node2D
{
	[Export] public string[] LineKeys = { };
	[Export] public NodePath[] Speakers = { };
	// true: the lines are one back-and-forth, played in order; false: one random line each time.
	[Export] public bool Conversation = true;
	// Seconds of silence between exchanges (or between barks), picked in this range.
	[Export] public float MinInterval = 8f;
	[Export] public float MaxInterval = 16f;
	// Pause between two lines of the same exchange.
	[Export] public float LineGap = 0.5f;
	// Only starts talking when the player is this close; the sound itself carries to MaxDistance.
	[Export] public float HearDistance = 700f;
	[Export] public float MaxDistance = 900f;
	[Export] public float VolumeDb = -4f;
	[Export] public bool ShowSubtitles = true;
	[Export] public float SubtitleHeight = 95f;

	private readonly RandomNumberGenerator _rng = new();
	private float _cooldown;
	private bool _busy;
	private int _lastBark = -1;

	public override void _Ready()
	{
		_rng.Randomize();
		// A little stagger so several sources in one scene don't all start at once.
		_cooldown = _rng.RandfRange(1.5f, MinInterval * 0.6f);
	}

	public override void _Process(double delta)
	{
		if (_busy || LineKeys.Length == 0)
			return;
		_cooldown -= (float)delta;
		if (_cooldown > 0f)
			return;
		_cooldown = _rng.RandfRange(MinInterval, MaxInterval);
		if (!PlayerIsNear() || DialogueBox.Instance?.IsOpen == true)
			return;
		_ = Talk();
	}

	private bool PlayerIsNear() =>
		GetTree().GetFirstNodeInGroup("player") is Node2D player
		&& player.GlobalPosition.DistanceTo(GlobalPosition) <= HearDistance;

	// Several exchanges in one list: a "-" entry ends one and starts the next; each Talk plays the
	// next exchange, cycling.
	private int _nextExchangeStart;

	private async Task Talk()
	{
		_busy = true;
		if (Conversation)
		{
			int start = _nextExchangeStart < LineKeys.Length ? _nextExchangeStart : 0;
			int end = System.Array.IndexOf(LineKeys, "-", start);
			if (end < 0)
				end = LineKeys.Length;
			_nextExchangeStart = end + 1 >= LineKeys.Length ? 0 : end + 1;
			// Someone in this exchange is gone or dead (a bandit you killed): the others don't answer
			// a corpse — skip the whole exchange.
			bool EveryoneAlive()
			{
				for (int i = start; i < end; i++)
					if (!CanSpeak(SpeakerFor(i)))
						return false;
				return true;
			}
			// Checked before every line, so someone dying mid-exchange also ends it there.
			for (int i = start; i < end && EveryoneAlive(); i++)
			{
				// The player opened a real conversation: let the rest of this exchange go.
				if (!IsInsideTree() || DialogueBox.Instance?.IsOpen == true)
					break;
				await SayLine(i);
				if (IsInsideTree())
					await ToSignal(GetTree().CreateTimer(LineGap), SceneTreeTimer.SignalName.Timeout);
			}
		}
		else
		{
			int index = _rng.RandiRange(0, LineKeys.Length - 1);
			if (LineKeys[index] == "-")
				index = (index + 1) % LineKeys.Length;
			if (LineKeys.Length > 1 && index == _lastBark)
				index = (index + 1) % LineKeys.Length;
			_lastBark = index;
			await SayLine(index);
		}
		_busy = false;
	}

	private Node2D SpeakerFor(int index)
	{
		if (index < Speakers.Length && Speakers[index] is NodePath path && !path.IsEmpty)
			return GetNodeOrNull<Node2D>(path);
		return this;
	}

	// Freed, out of the tree, or a character whose Stats say it's dead (enemies leave their body
	// in the scene after the death animation).
	private static bool CanSpeak(Node2D speaker)
	{
		if (!IsInstanceValid(speaker) || !speaker.IsInsideTree())
			return false;
		return speaker.GetNodeOrNull<Stats>("Stats") is not Stats stats || stats.CurrentHealth > 0;
	}

	private async Task SayLine(int index)
	{
		Node2D speaker = SpeakerFor(index);
		if (!CanSpeak(speaker))
			return;
		AudioStream stream = Sfx.LoadVoice(LineKeys[index]);
		float length = stream is null ? 2.5f : (float)stream.GetLength();

		if (stream is not null)
		{
			var player = new AudioStreamPlayer2D
			{
				Stream = stream, MaxDistance = MaxDistance, Attenuation = 1.6f, PanningStrength = 0.5f, VolumeDb = VolumeDb,
			};
			speaker.AddChild(player);
			player.Position = new Vector2(0f, -40f);
			player.Finished += player.QueueFree;
			player.Play();
		}
		if (ShowSubtitles)
			ShowSubtitle(speaker, LineKeys[index], length);

		await ToSignal(GetTree().CreateTimer(length), SceneTreeTimer.SignalName.Timeout);
	}

	// Small outlined line over the speaker's head, fading in and out with the clip. It lives in the
	// scene (not under the speaker) so a scaled or mirrored speaker doesn't stretch or flip the text,
	// and follows the speaker each frame. SubtitleHeight is measured from the speaker's origin.
	private void ShowSubtitle(Node2D speaker, string key, float length)
	{
		if (GetTree().CurrentScene is not Node scene)
			return;
		var label = new Label
		{
			Text = TranslationServer.Translate(key),
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(260f, 0f),
			Size = new Vector2(260f, 0f),
			Modulate = new Color(1f, 1f, 1f, 0f),
			ZIndex = 20,
			AutoTranslateMode = AutoTranslateModeEnum.Disabled,
		};
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.78f));
		label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
		label.AddThemeConstantOverride("outline_size", 4);
		label.GrowVertical = Control.GrowDirection.Begin;
		scene.AddChild(label);
		void Follow()
		{
			if (IsInstanceValid(speaker) && IsInstanceValid(label))
				label.GlobalPosition = speaker.GlobalPosition + new Vector2(-130f, -SubtitleHeight);
		}
		Follow();
		Callable follow = Callable.From(Follow);
		GetTree().Connect(SceneTree.SignalName.ProcessFrame, follow);
		label.TreeExiting += () =>
		{
			if (GetTree() is SceneTree tree && tree.IsConnected(SceneTree.SignalName.ProcessFrame, follow))
				tree.Disconnect(SceneTree.SignalName.ProcessFrame, follow);
		};

		Tween tween = label.CreateTween();
		tween.TweenProperty(label, "modulate:a", 1f, 0.2f);
		tween.TweenInterval(Mathf.Max(0.4f, length - 0.2f));
		tween.TweenProperty(label, "modulate:a", 0f, 0.4f);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}
}
