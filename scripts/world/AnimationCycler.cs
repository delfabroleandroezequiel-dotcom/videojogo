using Godot;

namespace Metroidvania.World;

// Test-room helper: loops an AnimatedSprite2D through a fixed list of animations, one at a
// time, so every animation a character has can be watched without needing input or AI to
// trigger it. Not meant for anything beyond EnemyLookAll-style review scenes.
public partial class AnimationCycler : Node
{
	[Export] public string[] Animations = { "idle" };
	[Export] public float SecondsPerAnimation = 2.5f;
	[Export] public NodePath SpritePath = "../Sprite";
	[Export] public NodePath LabelPath = "../Label";

	private AnimatedSprite2D _sprite;
	private Label _label;
	private int _index;
	private float _timer;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>(SpritePath);
		_label = GetNodeOrNull<Label>(LabelPath);
		PlayCurrent();
	}

	public override void _Process(double delta)
	{
		if (Animations.Length <= 1)
			return;

		_timer -= (float)delta;
		if (_timer <= 0f)
		{
			_index = (_index + 1) % Animations.Length;
			PlayCurrent();
		}
	}

	private void PlayCurrent()
	{
		_timer = SecondsPerAnimation;
		string anim = Animations[_index];
		if (_sprite.SpriteFrames.HasAnimation(anim))
			_sprite.Play(anim);

		if (_label is not null)
			_label.Text = anim;
	}
}
