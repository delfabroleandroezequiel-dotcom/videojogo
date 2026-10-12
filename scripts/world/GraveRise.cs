using System.Threading.Tasks;
using Godot;
using Metroidvania.Shared;

namespace Metroidvania.World;

// The "buried until you come close" state the KBPixelArt grave skeletons share (GraveSwordsman,
// GraveElite, GraveArbalist): they wait as a little dirt mound (digout frame 0), can't be hit, and
// claw their way out when the player gets near. Also runs the same clip backwards to sink again.
// Only the state + clip; what each skeleton does once it's up stays in its own class.
public sealed class GraveRise
{
	public bool Buried { get; private set; }
	public bool Moving { get; private set; }
	// Hits are ignored and the AI is off while this is true.
	public bool Busy => Buried || Moving;

	public void Bury(AnimatedSprite2D sprite)
	{
		Buried = true;
		sprite.Play("digout");
		sprite.Pause();
		sprite.Frame = 0;
	}

	// Called every physics frame while buried: starts the rise once the player is within reach.
	public void WatchForPlayer(Node2D self, AnimatedSprite2D sprite, float wakeDistance, float wakeHeight, float duration)
	{
		if (!Buried || Moving || PlayerReads.Find(self) is not { } player)
			return;
		Vector2 d = player.GlobalPosition - self.GlobalPosition;
		if (Mathf.Abs(d.X) <= wakeDistance && Mathf.Abs(d.Y) <= wakeHeight)
			_ = Rise(self, sprite, duration);
	}

	public async Task Rise(Node2D self, AnimatedSprite2D sprite, float duration)
	{
		Buried = true;
		Moving = true;
		int frames = sprite.SpriteFrames.GetFrameCount("digout");
		sprite.Play("digout");
		sprite.Pause();
		GroundSlamFx.Chip(self, self.GlobalPosition + new Vector2(0f, BodyBottom(self)), 0.4f);
		for (int f = 0; f < frames; f++)
		{
			sprite.Frame = f;
			if (!await Wait(self, duration / frames))
				return;
		}
		Buried = false;
		Moving = false;
	}

	// The rise backwards: back into the ground (ends Buried, hidden at frame 0).
	public async Task Sink(Node2D self, AnimatedSprite2D sprite, float duration)
	{
		Moving = true;
		int frames = sprite.SpriteFrames.GetFrameCount("digout");
		sprite.Play("digout");
		sprite.Pause();
		for (int f = frames - 1; f >= 0; f--)
		{
			sprite.Frame = f;
			if (!await Wait(self, duration / frames))
				return;
		}
		GroundSlamFx.Chip(self, self.GlobalPosition + new Vector2(0f, BodyBottom(self)), 0.4f);
		Buried = true;
		Moving = false;
	}

	private static float BodyBottom(Node2D self) =>
		self.GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape is RectangleShape2D rect ? rect.Size.Y * 0.5f : 28f;

	private static async Task<bool> Wait(Node2D self, float seconds)
	{
		await self.ToSignal(self.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
		return GodotObject.IsInstanceValid(self) && !self.IsQueuedForDeletion();
	}
}
