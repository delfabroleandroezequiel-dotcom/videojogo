using System.Threading.Tasks;
using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Ullatorp's tavern, first visit: the hooded man (Matheo, unnamed) sits in the middle of the room.
// Walking past him, Delfrad "flashes" — white bursts, blur, a jolt — and wakes up somewhere else:
// the vision map (VisionScenePath, Artemisa's arena for now), at his normal health, where she kills him.
// That map is a LevelBootstrap vision (VisionReturnScenePath): the death sends him back here, and
// this node then plays the hooded man's lines. Scene-owned, not an autoload: the only state that
// has to cross the two scene changes is the two statics below.
public partial class VisionMatheo : Node2D
{
	// Set by LevelBootstrap.ReturnFromVision, read once by the next VisionMatheo that loads.
	public static bool PendingReturn;
	// The player's health before the vision; restored on the way back (the vision death costs nothing).
	public static int? HealthBefore;

	[Export] public NodePath MatheoPath;
	// The flash fires when the player gets this close (in X) to Matheo.
	[Export] public float TriggerDistance = 70f;
	[Export] public string CinematicId = "vision_matheo";
	[Export(PropertyHint.File, "*.tscn")] public string VisionScenePath = "";
	[Export] public Vector2 VisionSpawn = Vector2.Zero;
	// Marked as seen before going in, so Artemisa skips her long entrance and just rises and attacks.
	[Export] public string VisionBossEntranceId = "vision_matheo_artemisa";
	[Export] public string SpeakerKey = "NPC_MATHEO_ENCAPUCHADO";
	[Export] public string[] ReturnLines = { };
	[Export] public float[] ReturnPauses = { };

	private Node2D _matheo;
	private Metroidvania.Player.Player _player;
	private bool _armed;

	public override void _Ready()
	{
		_matheo = GetNodeOrNull<Node2D>(MatheoPath);
		_player = GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;
		if (PendingReturn)
		{
			PendingReturn = false;
			_ = PlayReturn();
			return;
		}
		_armed = !SaveManager.Instance.IsCutscenePlayed(CinematicId) && _matheo is not null;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_armed || _player is null || Cinematic.Playing)
			return;
		if (Mathf.Abs(_player.GlobalPosition.X - _matheo.GlobalPosition.X) > TriggerDistance)
			return;
		_armed = false;
		_ = PlayFlash();
	}

	private async Task PlayFlash()
	{
		var scene = new Cinematic { Name = "VisionFlash", PlayOnce = true, PersistenceId = CinematicId, Skippable = false };
		var flash = new FlashLayer();
		AddChild(flash);
		scene.Steps.Add(new StepFace { Target = _matheo.GetPath() });
		scene.Steps.Add(new StepWait { Seconds = 0.25f });
		scene.Steps.Add(new StepCode { Action = () => flash.Burst(0.06f, 0.85f) });
		scene.Steps.Add(new StepShake { Strength = 5f, Duration = 0.25f });
		scene.Steps.Add(new StepBlur { Strength = 0.5f, Duration = 0.5f });
		scene.Steps.Add(new StepWait { Seconds = 0.35f });
		scene.Steps.Add(new StepCode { Action = () => flash.Burst(0.05f, 0.6f) });
		scene.Steps.Add(new StepWait { Seconds = 0.2f });
		scene.Steps.Add(new StepCode { Action = () => flash.Burst(0.04f, 1f, hold: true) });
		scene.Steps.Add(new StepShake { Strength = 8f, Duration = 0.4f, WaitForEnd = true });
		scene.Steps.Add(new StepCode { Action = () => { GoToVision(); return Task.CompletedTask; } });
		AddChild(scene);
		await scene.Play();
	}

	private void GoToVision()
	{
		HealthBefore = SaveManager.Instance.SessionCurrentHealth
			?? _player?.GetNode<Stats>("Stats").CurrentHealth;
		if (!string.IsNullOrEmpty(VisionBossEntranceId))
			SaveManager.Instance.MarkCutscenePlayed(VisionBossEntranceId);
		SaveManager.Instance.PendingSpawnPosition = VisionSpawn;
		SaveManager.Instance.PendingSpawnOnLadder = false;
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, VisionScenePath);
	}

	private async Task PlayReturn()
	{
		if (_matheo is null || ReturnLines.Length == 0)
			return;
		await ToSignal(GetTree().CreateTimer(0.4f), Timer.SignalName.Timeout);
		var scene = new Cinematic { Name = "VisionVuelta", PlayOnce = false };
		scene.Steps.Add(new StepBlur { Strength = 0.6f, Duration = 0.01f });
		scene.Steps.Add(new StepFace { Target = _matheo.GetPath() });
		scene.Steps.Add(new StepBlur { Strength = 0f, Duration = 1.4f });
		scene.Steps.Add(new StepCamera { Target = _matheo.GetPath(), Weight = 0.5f, Zoom = 1.25f, WaitSeconds = 0.8f });
		// First line, then the camera leans in on him during the silence and something glints under
		// the tunic (the Amuleto, never shown or named) — then the throwaway last line. Nothing is
		// explained and Delfrad doesn't ask: the player is left with "what was that?".
		scene.Steps.Add(new StepSay { SpeakerKeys = new[] { SpeakerKey }, LineKeys = new[] { ReturnLines[0] }, ExtraPauses = ReturnPauses.Length > 0 ? new[] { ReturnPauses[0] } : null });
		if (ReturnLines.Length > 1)
		{
			scene.Steps.Add(new StepCamera { Target = _matheo.GetPath(), Weight = 1f, Zoom = 1.7f, WaitSeconds = 0.5f });
			scene.Steps.Add(new StepCode { Action = () => Glint() });
			scene.Steps.Add(new StepSay
			{
				SpeakerKeys = new[] { SpeakerKey },
				LineKeys = ReturnLines[1..],
				ExtraPauses = ReturnPauses.Length > 1 ? ReturnPauses[1..] : null,
			});
		}
		scene.Steps.Add(new StepCamera { WaitSeconds = 0.4f });
		AddChild(scene);
		await scene.Play();
		if (IsInstanceValid(scene))
			scene.QueueFree();
	}

	// A faint warm glint on his chest that swells and dies in ~1.6 s (the hidden Amuleto).
	[Export] public Vector2 GlintOffset = new(2f, -10f);
	[Export] public Color GlintColor = new(1f, 0.6f, 0.25f, 1f);

	private async Task Glint()
	{
		var light = new PointLight2D
		{
			Texture = GD.Load<Texture2D>("res://resources/lighting/PointLightGradient.tres"),
			Color = GlintColor,
			TextureScale = 0.12f,
			Energy = 0f,
			Position = GlintOffset,
		};
		_matheo.AddChild(light);
		Tween tween = light.CreateTween();
		tween.TweenProperty(light, "energy", 1.6f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(light, "energy", 0f, 1.1f).SetTrans(Tween.TransitionType.Sine);
		await ToSignal(tween, Tween.SignalName.Finished);
		light.QueueFree();
	}

	// Full-screen white bursts over everything (the "flash" of the vision).
	private partial class FlashLayer : CanvasLayer
	{
		private ColorRect _rect;

		public override void _Ready()
		{
			Layer = 120;
			_rect = new ColorRect { Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
			_rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			AddChild(_rect);
		}

		public async Task Burst(float rise, float peak, bool hold = false)
		{
			Tween tween = CreateTween();
			tween.TweenProperty(_rect, "color:a", peak, rise);
			if (!hold)
				tween.TweenProperty(_rect, "color:a", 0f, 0.18f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
	}
}
