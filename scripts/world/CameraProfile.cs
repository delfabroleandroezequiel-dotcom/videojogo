using Godot;

namespace Metroidvania.World;

[GlobalClass]
public partial class CameraProfile : Resource
{
	[Export] public float Zoom = 1.5f;
	[Export] public float SmoothingSpeed = 5f;
	[Export] public float OffsetY = 0f;
}
