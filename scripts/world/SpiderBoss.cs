using Godot;

namespace Metroidvania.World;

// Boss.cs already provides the generic wander/retreat/lunge/combo/enrage chassis (see BossLobo).
// This only adds what makes THIS boss a spider: a ranged web-spit alongside its melee/lunge, and
// summoning spiderling reinforcements at HP breakpoints (rather than a repeating timer) so adds
// read as a deliberate phase change instead of an infinite spam mechanic.
public partial class SpiderBoss : Boss
{
	[Export] public PackedScene WebProjectileScene;
	[Export] public float WebSpitRange = 280f;
	[Export] public float WebSpitCooldown = 3.5f;
	[Export] public float WebSpitReleaseDelay = 0.3f;
	[Export] public float WebSpitRecoverDuration = 0.3f;
	[Export] public float WebProjectileSpeed = 220f;

	[Export] public PackedScene SpiderlingScene;
	[Export] public int SpiderlingsPerSummon = 2;
	[Export] public float SpiderlingSpreadX = 70f;
	[Export] public float[] SummonHealthThresholds = { 0.66f, 0.33f };

	private float _webSpitCooldownTimer;
	private bool _isWebSpitting;
	private int _nextSummonIndex;

	public override void _Ready()
	{
		base._Ready();
		if (IsQueuedForRemoval)
			return;

		_webSpitCooldownTimer = WebSpitCooldown * 0.5f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsQueuedForRemoval)
			return;

		base._PhysicsProcess(delta);
		if (IsQueuedForRemoval)
			return;

		CheckSummonThreshold();

		_webSpitCooldownTimer -= (float)delta;
		if (_isWebSpitting || _webSpitCooldownTimer > 0f || WebProjectileScene is null)
			return;

		Node2D player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player is null)
			return;

		float distance = Mathf.Abs(player.GlobalPosition.X - GlobalPosition.X);
		if (distance <= WebSpitRange)
			SpitWeb(player.GlobalPosition);
	}

	protected override void UpdateAnimation(Vector2 velocity)
	{
		if (_isWebSpitting)
		{
			if (Sprite is not null && Sprite.Animation != "attack")
				Sprite.Play("attack");
			return;
		}

		base.UpdateAnimation(velocity);
	}

	private void CheckSummonThreshold()
	{
		if (SpiderlingScene is null || _nextSummonIndex >= SummonHealthThresholds.Length)
			return;

		float threshold = SummonHealthThresholds[_nextSummonIndex];
		if (Stats.CurrentHealth > Stats.MaxHealth * threshold)
			return;

		_nextSummonIndex++;
		SummonSpiderlings();
	}

	private void SummonSpiderlings()
	{
		for (int i = 0; i < SpiderlingsPerSummon; i++)
		{
			Node spiderling = SpiderlingScene.Instantiate();
			GetTree().CurrentScene.AddChild(spiderling);

			if (spiderling is Node2D spiderling2D)
			{
				float side = i % 2 == 0 ? 1f : -1f;
				float spread = SpiderlingSpreadX * (1 + i / 2);
				spiderling2D.GlobalPosition = GlobalPosition + new Vector2(side * spread, 0f);
			}
		}
	}

	private async void SpitWeb(Vector2 targetPosition)
	{
		_isWebSpitting = true;
		_webSpitCooldownTimer = WebSpitCooldown;

		await ToSignal(GetTree().CreateTimer(WebSpitReleaseDelay), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || IsQueuedForRemoval)
			return;

		Projectile web = WebProjectileScene.Instantiate<Projectile>();
		GetTree().CurrentScene.AddChild(web);
		web.GlobalPosition = GlobalPosition;
		web.Speed = WebProjectileSpeed;
		web.Launch(targetPosition - GlobalPosition, Stats);

		await ToSignal(GetTree().CreateTimer(WebSpitRecoverDuration), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(this))
			_isWebSpitting = false;
	}
}
