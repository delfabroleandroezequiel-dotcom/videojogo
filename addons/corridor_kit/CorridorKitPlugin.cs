using Godot;

namespace Metroidvania.EditorTools;

// Editor-only convenience: select a CorridorBlock instance (or anything with an "Exit"
// Marker2D child) and press "+ Pasillo" to duplicate it and drop the copy exactly on the
// selected piece's Exit position, instead of copying Exit's numbers into the new piece by hand.
[Tool]
public partial class CorridorKitPlugin : EditorPlugin
{
	private Button _button;

	public override void _EnterTree()
	{
		_button = new Button { Text = "+ Pasillo (Exit)" };
		_button.Pressed += OnPressed;
		AddControlToContainer(CustomControlContainer.CanvasEditorMenu, _button);
	}

	public override void _ExitTree()
	{
		RemoveControlFromContainer(CustomControlContainer.CanvasEditorMenu, _button);
		_button.QueueFree();
	}

	private void OnPressed()
	{
		EditorSelection selection = EditorInterface.Singleton.GetSelection();
		Godot.Collections.Array<Node> selected = selection.GetSelectedNodes();

		if (selected.Count != 1 || selected[0] is not Node2D source)
		{
			GD.PushWarning("Corridor Kit: selecciona una sola pieza en el árbol (con un marcador 'Exit').");
			return;
		}

		Marker2D exit = source.GetNodeOrNull<Marker2D>("Exit");
		Node parent = source.GetParent();
		if (exit is null || parent is null)
		{
			GD.PushWarning("Corridor Kit: el nodo seleccionado no tiene un marcador 'Exit' (¿es un CorridorBlock?).");
			return;
		}

		Node2D duplicate = (Node2D)source.Duplicate();
		parent.AddChild(duplicate);
		duplicate.Owner = parent.Owner ?? parent;
		duplicate.GlobalPosition = exit.GlobalPosition;

		selection.Clear();
		selection.AddNode(duplicate);
	}
}
