using Godot;
using Metroidvania.Quests;
using Metroidvania.Save;
using Metroidvania.Shared;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class Npc : CharacterBody2D
{
	[Export] public string NpcName = "NPC_DEFAULT_NAME";
	[Export] public string[] DialogueLines = { "NPC_DEFAULT_LINE" };
	[Export] public string[] InProgressDialogueLines = { "NPC_DEFAULT_IN_PROGRESS" };
	[Export] public string[] CompletedDialogueLines = { "NPC_DEFAULT_COMPLETED" };
	[Export] public NpcDialogueStage[] StageDialogues = { };
	[Export] public Quest Quest;
	[Export] public bool GrantsOwnObjective = true;
	[Export] public float Gravity = 900f;
	[Export] public float ExplosionScale = 1f;
	[Export] public PackedScene ExplosionScene;
	[Export] public float KnockbackDuration = 0.2f;
	[Export] public SpriteFrames CustomSpriteFrames;

	private Node2D _visual;
	private AnimatedSprite2D _characterSprite;
	private Label _interactPrompt;
	private bool _playerInRange;
	private bool _isDead;
	private string _persistenceId;
	private float _knockbackTimer;
	private Vector2 _knockbackVelocity;

	public override void _Ready()
	{
		_persistenceId = GetPath().ToString();
		if (SaveManager.Instance.IsCommonEnemyDefeated(_persistenceId))
		{
			QueueFree();
			return;
		}

		Stats stats = GetNode<Stats>("Stats");
		stats.Died += OnDied;

		StatBar healthBar = GetNode<StatBar>("HealthBar");
		StatBar staminaBar = GetNode<StatBar>("StaminaBar");
		healthBar.Visible = false;
		staminaBar.Visible = false;
		stats.HealthChanged += (current, max) => healthBar.SetRatio((float)current / max);
		stats.StaminaChanged += (current, max) => staminaBar.SetRatio((float)current / max);

		_visual = GetNode<Node2D>("Visual");
		_characterSprite = GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		if (CustomSpriteFrames is not null)
		{
			_characterSprite.SpriteFrames = CustomSpriteFrames;
			_characterSprite.Play("idle");
		}

		_interactPrompt = GetNode<Label>("InteractPrompt");
		_interactPrompt.Visible = false;

		Area2D interactArea = GetNode<Area2D>("InteractArea");
		interactArea.BodyEntered += OnInteractAreaEntered;
		interactArea.BodyExited += OnInteractAreaExited;

		if (Quest is not null)
			QuestManager.Instance.RegisterQuest(Quest);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		Vector2 velocity = Velocity;

		if (_knockbackTimer > 0)
		{
			_knockbackTimer -= (float)delta;
			velocity.X = _knockbackVelocity.X;
			velocity.Y = IsOnFloor() ? 0 : velocity.Y + Gravity * (float)delta;
		}
		else
		{
			velocity.X = 0;
			velocity.Y = IsOnFloor() ? 0 : velocity.Y + Gravity * (float)delta;
		}

		Velocity = velocity;
		MoveAndSlide();

		FacePlayer();
	}

	private void FacePlayer()
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Node2D player)
			return;

		float facingX = player.GlobalPosition.X - GlobalPosition.X;
		if (Mathf.Abs(facingX) > 0.01f)
			_visual.Scale = new Vector2(facingX >= 0 ? 1 : -1, 1);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_isDead || !_playerInRange || DialogueBox.Instance.IsOpen)
			return;

		if (@event.IsActionPressed("interact"))
		{
			Interact();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnInteractAreaEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = true;
		_interactPrompt.Visible = !_isDead;
	}

	private void OnInteractAreaExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		_playerInRange = false;
		_interactPrompt.Visible = false;
	}

	private void Interact()
	{
		if (Quest is null)
		{
			DialogueBox.Instance.Show(NpcName, GetCurrentDialogueLines());
			return;
		}

		if (QuestManager.Instance.IsCompleted(Quest.Id))
		{
			DialogueBox.Instance.Show(NpcName, CompletedDialogueLines);
			return;
		}

		if (QuestManager.Instance.IsActive(Quest.Id))
		{
			if (QuestManager.Instance.IsObjectiveMet(Quest))
			{
				QuestManager.Instance.CompleteQuest(Quest.Id);
				DialogueBox.Instance.Show(NpcName, CompletedDialogueLines);
			}
			else
			{
				DialogueBox.Instance.Show(NpcName, InProgressDialogueLines);
			}

			return;
		}

		QuestManager.Instance.StartQuest(Quest);
		if (GrantsOwnObjective)
			QuestManager.Instance.AddProgress(Quest.Id);

		if (QuestManager.Instance.IsObjectiveMet(Quest))
		{
			QuestManager.Instance.CompleteQuest(Quest.Id);
			DialogueBox.Instance.Show(NpcName, CompletedDialogueLines);
		}
		else
		{
			DialogueBox.Instance.Show(NpcName, DialogueLines);
		}
	}

	private string[] GetCurrentDialogueLines()
	{
		if (StageDialogues is null || StageDialogues.Length == 0)
			return DialogueLines;

		int currentStage = SaveManager.Instance.StoryStage;
		NpcDialogueStage best = null;

		foreach (NpcDialogueStage stage in StageDialogues)
		{
			if (stage.MinStage <= currentStage && (best is null || stage.MinStage > best.MinStage))
				best = stage;
		}

		return best?.Lines ?? DialogueLines;
	}

	public void ApplyKnockback(Vector2 direction, float force)
	{
		_knockbackVelocity = direction * force;
		_knockbackTimer = KnockbackDuration;
	}

	private void OnDied()
	{
		_isDead = true;
		_interactPrompt.Visible = false;
		SaveManager.Instance.MarkCommonEnemyDefeated(_persistenceId);

		if (ExplosionScene is not null)
		{
			Node explosionNode = ExplosionScene.Instantiate();
			if (explosionNode is Explosion explosion)
				explosion.TargetScale = ExplosionScale;

			GetTree().CurrentScene.AddChild(explosionNode);
			((Node2D)explosionNode).GlobalPosition = GlobalPosition;
		}

		QueueFree();
	}
}
