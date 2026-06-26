using Godot;
using Metroidvania.Save;
using Metroidvania.UI;

namespace Metroidvania.World;

public partial class SavePoint : Area2D
{
	[Export] public Vector2 RespawnOffset = new(-50, 0);

	private string _persistenceId;
	private bool _isLit;
	private Node2D _unlitVisual;
	private Node2D _litVisual;

	public override void _Ready()
	{
		_persistenceId = GetPath().ToString();
		_unlitVisual = GetNode<Node2D>("UnlitVisual");
		_litVisual = GetNode<Node2D>("LitVisual");

		_isLit = SaveManager.Instance.IsSavePointLit(_persistenceId);
		UpdateVisual();

		BodyEntered += OnBodyEntered;
	}

	private void UpdateVisual()
	{
		_unlitVisual.Visible = !_isLit;
		_litVisual.Visible = _isLit;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (!_isLit)
			ConfirmPrompt.Instance.Show("¿Desea encenderla?", OnLightConfirmed);
		else
			ConfirmPrompt.Instance.Show("¿Desea descansar?", Rest);
	}

	private void OnLightConfirmed()
	{
		_isLit = true;
		SaveManager.Instance.MarkSavePointLit(_persistenceId);
		UpdateVisual();
		Rest();
	}

	private void Rest()
	{
		if (GetTree().CurrentScene is LevelBootstrap level)
			level.SaveAtCheckpoint(GlobalPosition + RespawnOffset);
	}
}
