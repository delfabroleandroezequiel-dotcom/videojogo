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

	public Metroidvania.Player.Player Player { get; private set; }

	private readonly HashSet<Npc> _controlled = new();
	private CanvasLayer _fadeLayer;
	private ColorRect _fadeRect;
	private ShaderMaterial _blurMaterial;

	public override void _Ready()
	{
		if (PlayOnStart)
			Callable.From(() => { _ = Play(); }).CallDeferred();
	}

	private string Id => string.IsNullOrEmpty(PersistenceId) ? GetPath().ToString() : PersistenceId;

	public async Task Play()
	{
		if (Playing || (PlayOnce && SaveManager.Instance.IsCutscenePlayed(Id)))
			return;
		Playing = true;
		Player = GetTree().GetFirstNodeInGroup("player") as Metroidvania.Player.Player;
		Player?.HoldForCutscene(true);
		try
		{
			foreach (CinematicStep step in Steps)
			{
				if (step is null)
					continue;
				await step.Run(this);
				if (!IsInstanceValid(this))
					return;
			}
		}
		finally
		{
			Playing = false;
			if (IsInstanceValid(this))
				Release();
		}
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
