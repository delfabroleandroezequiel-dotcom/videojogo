using Godot;
using Metroidvania.Player;
using Metroidvania.Shared;

namespace Metroidvania.World;

// Young Delfrad, for scenes set before the journey (Arthur's training): the everyday palette with
// the armour painted as bare skin and the blade as a wooden sword, a slightly smaller figure,
// wooden-sword damage and only the basic kit. A plain node owned by the scene that wants it;
// nothing is saved, so the next scene's player is the normal one again.
public partial class ModoJoven : Node
{
	[Export] public Material Palette;
	[Export] public float ScaleFactor = 0.88f;
	// Sheet pixels from the sprite's centre down to the feet, so shrinking keeps them on the ground.
	[Export] public float FeetBelowCentre = 29f;
	[Export] public float DamageMultiplier = 0.5f;
	// No magic yet: the dash/jump afterimages go from the blue neon to a faint white.
	[Export] public Color GhostColor = new(1f, 1f, 1f, 1f);
	[Export] public float GhostAlpha = 0.4f;
	[Export] public float GhostIntensity = 0.9f;
	// The cloth cape from CharacterFlair comes later in his journey.
	[Export] public bool ShowCape = false;
	// A boy's voice instead of the grown man's grunts.
	[Export] public string VoiceCategory = "Voice/DelfradJoven";
	[Export] public string HurtVoiceKey = "Golpeado";
	[Export] public string[] LockedAbilities =
	{
		PlayerAbilities.Spell, PlayerAbilities.Spell2, PlayerAbilities.TornadoSpell, PlayerAbilities.PoisonSpell,
		PlayerAbilities.FireballSpell, PlayerAbilities.WindShield, PlayerAbilities.FlyingSword,
		PlayerAbilities.Torch, PlayerAbilities.ChargedAttack, PlayerAbilities.RunThrust, PlayerAbilities.PoundAttack,
	};

	public override void _Ready() => Callable.From(Apply).CallDeferred();

	private void Apply()
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Metroidvania.Player.Player player)
			return;

		if (Palette is not null)
			player.SetBasePalette(Palette);

		var sprite = player.GetNode<AnimatedSprite2D>("Visual/CharacterSprite");
		float before = sprite.Scale.Y;
		float after = before * ScaleFactor;
		sprite.Scale = new Vector2(sprite.Scale.X * ScaleFactor, after);
		sprite.Position += new Vector2(0f, FeetBelowCentre * (before - after));

		if (player.GetNodeOrNull<CharacterFlair>("CharacterFlair") is { } flair)
			flair.CapeEnabled = ShowCape;

		player.VoiceCategory = VoiceCategory;
		player.HurtVoiceKey = HurtVoiceKey;

		player.DashGhostColor = GhostColor;
		player.DashGhostAlpha = GhostAlpha;
		player.DashGhostIntensity = GhostIntensity;
		player.JumpGhostAlpha *= GhostAlpha / 0.85f;
		player.JumpGhostIntensity = GhostIntensity;

		var stats = player.GetNode<Stats>("Stats");
		stats.AttackPower = Mathf.Max(1, Mathf.RoundToInt(stats.AttackPower * DamageMultiplier));

		var abilities = player.GetNode<PlayerAbilities>("Abilities");
		foreach (string id in LockedAbilities)
			abilities.LockForScene(id);
	}
}
