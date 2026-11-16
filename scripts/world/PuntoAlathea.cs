using Godot;
using Metroidvania.Save;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Where Alathea turns up when the hunting horn is blown (book cap. 2: "Si me necesitas, soplas").
// Put one on a high spot of a boss arena: while BossId is alive and this spot hasn't answered yet
// in this visit, blowing the horn (Player.UseConsumable → TryCall) spawns an ArqueraTorre here that
// fires MaxShots arrows and leaves. One call per arena visit; nowhere else does anyone answer.
public partial class PuntoAlathea : Node2D
{
	public const string Group = "punto_alathea";

	[Export] public string BossId = "";
	[Export] public SpriteFrames Frames;
	[Export] public Vector2 SpriteScale = new(1.158f, 1.158f);
	[Export] public int MaxShots = 6;
	[Export] public int Damage = 34;
	[Export] public float Range = 900f;

	private bool _used;

	public override void _Ready() => AddToGroup(Group);

	private bool CanAnswer => !_used && Frames is not null
		&& (string.IsNullOrEmpty(BossId) || !SaveManager.Instance.IsBossDefeated(BossId));

	// Called when the horn is blown. True if Alathea answered (the horn isn't used up either way).
	public static bool TryCall(Node2D caller)
	{
		foreach (Node n in caller.GetTree().GetNodesInGroup(Group))
		{
			if (n is PuntoAlathea punto && punto.CanAnswer)
			{
				punto.Answer();
				return true;
			}
		}
		FloatingSubtitle.Show(caller, "CUERNO_SIN_RESPUESTA", 2.2f, 70f);
		return false;
	}

	private void Answer()
	{
		_used = true;
		var archer = new ArqueraTorre
		{
			Frames = Frames,
			SpriteScale = SpriteScale,
			MaxShots = MaxShots,
			Damage = Damage,
			Range = Range,
			BossAliveId = BossId,
		};
		AddChild(archer);
		archer.FadeIn();
		FloatingSubtitle.Show(archer, "CUERNO_ALATHEA_LLEGA", 2.4f, 60f);
	}
}
