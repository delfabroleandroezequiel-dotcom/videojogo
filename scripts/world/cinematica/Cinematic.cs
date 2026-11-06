using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Metroidvania.Save;

namespace Metroidvania.World;

// A cinematic in the HoldForCutscene style: cinema bars, no player control, the camera guided — and
// the characters walking, animating and talking while the world keeps running (dialogue doesn't
// pause the game here). Built from a list of steps authored in the Inspector (CinematicStep
// resources), run in order. A plain node owned by the scene that uses it, not an autoload.
// Actor/target paths in the steps are relative to this node; an empty path means the player.
[GlobalClass]
public partial class Cinematic : Node
{
	// True while any cinematic runs: NPCs and triggers ignore Interact meanwhile.
	public static bool Playing { get; private set; }

	[Signal] public delegate void FinishedEventHandler();

	[Export] public Godot.Collections.Array<CinematicStep> Steps = new();
	[Export] public bool PlayOnStart;
	[Export] public bool PlayOnce = true;
	// Save key for PlayOnce; empty = this node's path.
	[Export] public string PersistenceId = "";
	// Only plays once this quest is completed ("" = always): a scene that belongs to a later point
	// of the story in a map the player visits before (Arthur's sword, in Casa1 after Morlok's cart).
	[Export] public string RequiresQuestCompleted = "";

	// Holding SkipAction for SkipHoldSeconds skips the rest: a short black, every remaining step's
	// end state applied at once (CinematicStep.Skip), and the cinematic still counts as seen, so
	// PlayOnce never brings it back. Always on by default (the player's time comes first); turn it
	// off only for a few seconds that can't be summed up.
	[Export] public bool Skippable = true;
	[Export] public string SkipAction = "jump";
	[Export] public float SkipHoldSeconds = 1.2f;

	public Metroidvania.Player.Player Player { get; private set; }

	// True from the moment the skip fires: steps that loop (walking, waiting on a voice) bail out.
	public bool Skipping { get; private set; }
	// Steps that leave something on screen when skipped clean it up here (a title card, its music).
	public event System.Action SkipStarted;
	// Where the screen ends after a skip: StepFade.Skip sets it (1 = stays black for a scene change).
	public float SkipFadeAlpha { get; set; }

	private readonly HashSet<Npc> _controlled = new();
	private CanvasLayer _fadeLayer;
	private ColorRect _fadeRect;
	private ShaderMaterial _blurMaterial;
	private bool _running;
	private TaskCompletionSource _skipRequest;
	private CinematicSkipIndicator _skipIndicator;
	private float _skipHeld;
	private float _skipHintTimer;
	// A button already down when the cinematic starts (jumping through a door) doesn't count.
	private bool _skipArmed;

	public override void _Ready()
	{
		if (PlayOnStart)
			Callable.From(() => { _ = Play(); }).CallDeferred();
	}

	private string Id => string.IsNullOrEmpty(PersistenceId) ? GetPath().ToString() : PersistenceId;

	public bool CanPlay =>
		!Playing
		&& !(PlayOnce && SaveManager.Instance.IsCutscenePlayed(Id))
		&& (string.IsNullOrEmpty(RequiresQuestCompleted) || Metroidvania.Quests.QuestManager.Instance.IsCompleted(RequiresQuestCompleted));

	public async Task Play()
	{
		if (!CanPlay)
			return;
		Playing = true;
		_running = true;
		Skipping = false;
		_skipRequest = new TaskCompletionSource();
		_skipHeld = 0f;
		_skipArmed = false;
		Player = GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;
		Player?.HoldForCutscene(true);
		try
		{
			for (int i = 0; i < Steps.Count; i++)
			{
				CinematicStep step = Steps[i];
				if (step is null)
					continue;
				Task running = step.Run(this);
				await Task.WhenAny(running, _skipRequest.Task);
				if (!IsInstanceValid(this))
					return;
				if (Skipping)
				{
					await SkipFrom(i);
					break;
				}
				await running; // surfaces a step's exception
			}
		}
		finally
		{
			Playing = false;
			_running = false;
			if (IsInstanceValid(this))
				Release();
		}
	}

	public override void _Process(double delta)
	{
		if (!_running || !Skippable || Skipping)
		{
			if (_skipIndicator is not null)
				_skipIndicator.SetShown(false, (float)delta);
			return;
		}
		float dt = (float)delta;
		bool held = InputMap.HasAction(SkipAction) && Input.IsActionPressed(SkipAction);
		if (!held)
			_skipArmed = true;
		if (held && _skipArmed)
			_skipHeld += dt;
		else
			_skipHeld = Mathf.MoveToward(_skipHeld, 0f, dt * 2f);
		_skipHintTimer = Mathf.Max(0f, _skipHintTimer - dt);

		bool show = _skipHeld > 0f || _skipHintTimer > 0f;
		if (show && _skipIndicator is null)
		{
			_skipIndicator = new CinematicSkipIndicator();
			AddChild(_skipIndicator);
		}
		if (_skipIndicator is not null)
		{
			_skipIndicator.Progress = _skipHeld / Mathf.Max(0.05f, SkipHoldSeconds);
			_skipIndicator.SetShown(show, dt);
		}
		if (_skipHeld >= SkipHoldSeconds)
			RequestSkip();
	}

	// Any button during a cinematic shows the hint for a moment, so the player learns it exists.
	public override void _Input(InputEvent @event)
	{
		if (_running && Skippable && !Skipping && @event.IsPressed() && !@event.IsEcho()
			&& @event is InputEventKey or InputEventJoypadButton or InputEventMouseButton)
			_skipHintTimer = 2.5f;
	}

	// Also callable from code (a debug key, a test).
	public void RequestSkip()
	{
		if (!_running || Skipping)
			return;
		Skipping = true;
		_skipRequest?.TrySetResult();
	}

	private async Task SkipFrom(int index)
	{
		SkipStarted?.Invoke();
		ColorRect rect = FadeRect();
		SkipFadeAlpha = rect.Color.A;
		Tween toBlack = rect.CreateTween();
		toBlack.TweenProperty(rect, "color:a", 1f, 0.25f);
		await ToSignal(toBlack, Tween.SignalName.Finished);
		if (!IsInstanceValid(this))
			return;
		// Whatever was talking stops now.
		if (Metroidvania.UI.DialogueBox.Instance is { } box)
			box.SkipToEnd();
		Metroidvania.Shared.Sfx.StopVoice();
		for (int i = index; i < Steps.Count; i++)
		{
			Steps[i]?.Skip(this);
			if (!IsInstanceValid(this))
				return;
		}
		await WaitPhysicsFrame();
		if (!IsInstanceValid(this) || SkipFadeAlpha >= 0.99f)
			return;
		Tween back = rect.CreateTween();
		back.TweenProperty(rect, "color:a", SkipFadeAlpha, 0.5f);
		await ToSignal(back, Tween.SignalName.Finished);
	}

	// A reload or scene change mid-cinematic frees this node while a step is awaiting, so the finally
	// above never runs: without this, Playing stays stuck and every later cinematic refuses to start.
	public override void _ExitTree()
	{
		if (_running)
			Playing = false;
	}

	private void Release()
	{
		foreach (Npc npc in _controlled)
		{
			if (!IsInstanceValid(npc))
				continue;
			npc.CinematicVelocityX = 0f;
			npc.CinematicControl = false;
		}
		_controlled.Clear();
		if (_skipIndicator is not null)
		{
			_skipIndicator.QueueFree();
			_skipIndicator = null;
		}
		_skipHintTimer = 0f;
		if (IsInstanceValid(Player))
			Player.HoldForCutscene(false);
		if (_fadeRect is not null && _fadeRect.Color.A <= 0.01f)
		{
			_fadeLayer.QueueFree();
			_fadeLayer = null;
			_fadeRect = null;
		}
		if (PlayOnce)
			SaveManager.Instance.MarkCutscenePlayed(Id);
		EmitSignal(SignalName.Finished);
	}

	// ── Helpers for the steps ───────────────────────────────────────────────────────────────

	public Node2D Resolve(NodePath path) =>
		path is null || path.IsEmpty ? Player : GetNodeOrNull<Node2D>(path);

	public void TakeControl(Npc npc)
	{
		npc.CinematicControl = true;
		_controlled.Add(npc);
	}

	public async Task WaitSeconds(float seconds)
	{
		if (seconds > 0f)
			await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	public async Task WaitPhysicsFrame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

	// Characters built like the rest (Visual + Visual/CharacterSprite): face a direction, play an anim.
	public static void FaceActor(Node2D actor, float direction)
	{
		if (actor?.GetNodeOrNull<Node2D>("Visual") is { } visual && Mathf.Abs(direction) > 0.01f)
			visual.Scale = new Vector2(direction > 0f ? 1 : -1, 1);
	}

	public static AnimatedSprite2D SpriteOf(Node2D actor) =>
		actor?.GetNodeOrNull<AnimatedSprite2D>("Visual/CharacterSprite");

	public ShaderMaterial BlurMaterial()
	{
		if (_blurMaterial is not null)
			return _blurMaterial;
		_blurMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://resources/shaders/BlurPantalla.gdshader") };
		_blurMaterial.SetShaderParameter("strength", 0f);
		var layer = new CanvasLayer { Layer = 4, Name = "DesenfoqueCinematica" };
		var rect = new ColorRect { Material = _blurMaterial, MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(rect);
		AddChild(layer);
		return _blurMaterial;
	}

	public ColorRect FadeRect()
	{
		if (_fadeRect is not null)
			return _fadeRect;
		_fadeLayer = new CanvasLayer { Layer = 60, Name = "FundidoCinematica" };
		_fadeRect = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_fadeLayer.AddChild(_fadeRect);
		AddChild(_fadeLayer);
		return _fadeRect;
	}
}
