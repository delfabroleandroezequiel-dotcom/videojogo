namespace Metroidvania.Shared;

// Bit values for the custom collision layers set up in Project Settings > Layer Names >
// 2D Physics (layer 5 = ClimbableWalls, layer 6 = OneWayPlatforms). Centralized here so every
// script that tags/checks these layers uses the same constant instead of re-deriving the
// bit shift in its own file.
public static class PhysicsLayers
{
	public const uint ClimbableWalls = 1u << 4;
	public const uint OneWayPlatforms = 1u << 5;
}
