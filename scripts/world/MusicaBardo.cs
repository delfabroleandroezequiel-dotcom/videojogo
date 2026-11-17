using Godot;

namespace Metroidvania.World;

// A street musician's songs: plays its tracks one after another as positional sound (loud next to
// him, fading with distance), with a short breather between songs like a real busker. Put it as a
// child of the musician so the sound comes from him.
public partial class MusicaBardo : Node2D
{
	[Export] public AudioStream[] Canciones = { };
	[Export] public float PausaMin = 2.5f;
	[Export] public float PausaMax = 6f;
	[Export] public float VolumeDb = -6f;
	[Export] public float MaxDistance = 900f;
	[Export] public float Attenuation = 1.4f;

	private AudioStreamPlayer2D _player;
	private readonly RandomNumberGenerator _rng = new();
	private int _index;
	private float _pause;

	public override void _Ready()
	{
		_rng.Randomize();
		_player = new AudioStreamPlayer2D
		{
			VolumeDb = VolumeDb, MaxDistance = MaxDistance, Attenuation = Attenuation, PanningStrength = 0.6f,
		};
		AddChild(_player);
		_player.Finished += () => _pause = _rng.RandfRange(PausaMin, PausaMax);
		if (Canciones.Length == 0)
			return;
		_index = _rng.RandiRange(0, Canciones.Length - 1);
		_player.Stream = Canciones[_index];
		// Join mid-song so walking into town doesn't always start at bar one.
		_player.Play(_rng.RandfRange(0f, (float)Canciones[_index].GetLength() * 0.6f));
	}

	public override void _Process(double delta)
	{
		if (_pause <= 0f || Canciones.Length == 0)
			return;
		_pause -= (float)delta;
		if (_pause > 0f)
			return;
		_index = (_index + 1) % Canciones.Length;
		_player.Stream = Canciones[_index];
		_player.Play();
	}
}
