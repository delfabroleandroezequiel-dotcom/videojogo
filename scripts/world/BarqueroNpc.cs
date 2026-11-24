using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

// An Npc that sells a boat trip: talks (GreetingLines), asks through ConfirmPrompt whether to sail
// to DestinationNameKey for Fare gold, and on a yes charges it and changes scene with the usual
// fade, dropping the player at DestinationSpawnPosition. Fare 0 = free trip (the way back), the
// question then leaves the price out. Not enough gold → NoGoldLines and nothing happens.
public partial class BarqueroNpc : Npc
{
	[Export] public int Fare = 100;
	[Export(PropertyHint.File, "*.tscn")] public string DestinationScenePath = "";
	[Export] public Vector2 DestinationSpawnPosition = Vector2.Zero;
	// Translation key of the place name shown in the question ("la Montaña Extraña").
	[Export] public string DestinationNameKey = "";
	[Export] public string[] GreetingLines = { };
	[Export] public string[] NoGoldLines = { };
	[Export] public string[] DeclineLines = { };
	[Export] public string[] DepartLines = { };
	// Overrides the default "sail to {0} for {1} coins?" question ({0} = place, {1} = fare) — the
	// samurai asking whether to train uses the same flow to take you to his arena.
	[Export] public string QuestionKey = "";

	protected override void Interact()
	{
		// Once the player holds AfterItemId the offer is over: fall back to the regular Npc talk
		// (AfterItemDialogueLines), e.g. the samurai after handing over the boots.
		bool offerDone = !string.IsNullOrEmpty(AfterItemId) && SaveManager.Instance.HasItem(AfterItemId);
		if (string.IsNullOrEmpty(DestinationScenePath) || offerDone)
		{
			base.Interact();
			return;
		}

		if (GreetingLines.Length > 0)
			DialogueBox.Instance.Show(NpcName, GreetingLines, AskToSail, this);
		else
			AskToSail();
	}

	private void AskToSail()
	{
		string place = TranslationServer.Translate(DestinationNameKey);
		string key = !string.IsNullOrEmpty(QuestionKey) ? QuestionKey
			: Fare > 0 ? "BARQUERO_PREGUNTA" : "BARQUERO_PREGUNTA_GRATIS";
		string question = string.Format(TranslationServer.Translate(key), place, Fare);
		ConfirmPrompt.Instance.Show(question, OnAccepted, OnDeclined);
	}

	private void OnAccepted()
	{
		if (Fare > 0 && !SaveManager.Instance.SpendGold(Fare))
		{
			if (NoGoldLines.Length > 0)
				DialogueBox.Instance.Show(NpcName, NoGoldLines, null, this);
			return;
		}

		if (DepartLines.Length > 0)
			DialogueBox.Instance.Show(NpcName, DepartLines, Sail, this);
		else
			Sail();
	}

	private void OnDeclined()
	{
		if (DeclineLines.Length > 0)
			DialogueBox.Instance.Show(NpcName, DeclineLines, null, this);
	}

	private void Sail()
	{
		SaveManager.Instance.PendingSpawnPosition = DestinationSpawnPosition;
		SaveManager.Instance.PendingSpawnOnLadder = false;
		SceneFader.ChangeSceneWithFade(GetTree(), DestinationScenePath);
	}
}
