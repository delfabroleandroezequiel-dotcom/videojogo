using Godot;

namespace Metroidvania.World;

// Showcase-only figure for the Roster map (trailer material, not gameplay): shows a character
// standing in its idle animation with its feet exactly on this node's origin, so placing it on a
// ledge is just dropping the node on the ledge line.
//
// Give it either Frames (a SpriteFrames resource, scaled by Scale), or Source (any enemy/boss/NPC
// scene): the scene is instantiated without ever entering the tree — no AI, physics or _Ready —
// and only its visible Sprite2D/AnimatedSprite2D nodes are copied, keeping their relative
// transforms (so the in-game scale, flips and multi-part sprites like the eye boss come along).
// The feet are found from the main sprite's actual opaque pixels across its idle frames, so the
// sheet's own padding doesn't matter. [Tool] so it previews in the editor; the preview nodes are
// never owned, so nothing is baked into the scene file.
[Tool]
public partial class RosterSlot : Node2D
{
	private PackedScene _source;
	private SpriteFrames _frames;
	private string _animation = "idle";
	private float _figureScale = 1f;
	private bool _mirror;
	private bool _initialized;

	[Export] public PackedScene Source { get => _source; set { _source = value; Rebuild(); } }
	[Export] public SpriteFrames Frames { get => _frames; set { _frames = value; Rebuild(); } }
	// Preferred idle animation; falls back to Idle/idle_1/default/the first one if missing.
	[Export] public string Animation { get => _animation; set { _animation = value; Rebuild(); } }
	// Frames: the figure's scale. Source: a multiplier on the scene's own in-game scale.
	[Export] public float FigureScale { get => _figureScale; set { _figureScale = value; Rebuild(); } }
	// Face the other way (the roster faces everyone toward the middle of the steps).
	[Export] public bool Mirror { get => _mirror; set { _mirror = value; Rebuild(); } }

	// The figure's visible bounds in this node's local space (feet at y = 0). Used by the
	// generator that lays the roster out.
	public Rect2 ContentRect { get; private set; }

	private static readonly string[] IdleFallbacks = { "idle", "Idle", "IDLE", "idle_1", "idle1", "default" };

	public override void _Ready()
	{
		_initialized = true;
		Rebuild();
	}

	private void Rebuild()
	{
		if (!_initialized || !IsInsideTree())
			return;

		Node old = GetNodeOrNull("_Preview");
		if (old is not null)
		{
			RemoveChild(old);
			old.QueueFree();
		}

		var outer = new Node2D { Name = "_Preview", Scale = new Vector2(_mirror ? -1f : 1f, 1f) };
		var figure = new Node2D { Name = "Figure" };
		outer.AddChild(figure);
		AddChild(outer);

		Node2D main = null;
		if (_frames is not null)
		{
			var sprite = new AnimatedSprite2D { SpriteFrames = _frames, Scale = Vector2.One * _figureScale };
			figure.AddChild(sprite);
			main = sprite;
		}
		else if (_source is not null)
		{
			Node instance = _source.Instantiate();
			main = CopySprites(instance, Transform2D.Identity, figure);
			instance.Free();
			figure.Scale = Vector2.One * _figureScale;
		}
		if (main is null)
		{
			ContentRect = new Rect2();
			return;
		}

		foreach (Node child in figure.GetChildren())
		{
			if (child is AnimatedSprite2D animated)
				PlayIdle(animated);
		}

		// Feet: bottom-centre of the main sprite's opaque pixels, in figure space.
		Rect2 content = MeasureContent(main);
		Vector2 feet = new(content.GetCenter().X, content.End.Y);
		Vector2 scale = figure.Scale;
		figure.Position = -feet * scale;
		ContentRect = new Rect2((content.Position - feet) * scale, content.Size * scale);
	}

	// Copies every visible sprite in the instance (skipping UI like stat bars) into `target`,
	// with transforms relative to the instance root. Returns the biggest one (the body).
	private static Node2D CopySprites(Node node, Transform2D parentTransform, Node2D target)
	{
		Node2D biggest = null;
		float biggestArea = 0f;

		void Walk(Node current, Transform2D xform)
		{
			if (current is Control || current is CanvasLayer)
				return;
			if (current is CanvasItem item && !item.Visible)
				return;
			if (current.Name.ToString().Contains("Bar"))
				return;

			Transform2D here = current is Node2D n2d ? xform * n2d.Transform : xform;
			Node2D copy = null;
			float area = 0f;
			switch (current)
			{
				case AnimatedSprite2D animated when animated.SpriteFrames is not null:
				{
					var c = new AnimatedSprite2D
					{
						SpriteFrames = animated.SpriteFrames,
						Centered = animated.Centered,
						Offset = animated.Offset,
						FlipH = animated.FlipH,
						FlipV = animated.FlipV,
						Modulate = animated.Modulate,
						SelfModulate = animated.SelfModulate,
						SpeedScale = animated.SpeedScale,
						Material = animated.Material,
						Animation = animated.Animation,
					};
					Texture2D t = FirstFrame(animated.SpriteFrames, animated.Animation);
					if (t is not null)
						area = t.GetWidth() * t.GetHeight() * Mathf.Abs(here.Scale.X * here.Scale.Y);
					copy = c;
					break;
				}
				case Sprite2D sprite when sprite.Texture is not null:
				{
					var c = new Sprite2D
					{
						Texture = sprite.Texture,
						Centered = sprite.Centered,
						Offset = sprite.Offset,
						FlipH = sprite.FlipH,
						FlipV = sprite.FlipV,
						Hframes = sprite.Hframes,
						Vframes = sprite.Vframes,
						Frame = sprite.Frame,
						RegionEnabled = sprite.RegionEnabled,
						RegionRect = sprite.RegionRect,
						Modulate = sprite.Modulate,
						SelfModulate = sprite.SelfModulate,
						Material = sprite.Material,
					};
					Vector2 size = sprite.RegionEnabled ? sprite.RegionRect.Size
						: sprite.Texture.GetSize() / new Vector2(sprite.Hframes, sprite.Vframes);
					area = size.X * size.Y * Mathf.Abs(here.Scale.X * here.Scale.Y);
					copy = c;
					break;
				}
			}

			if (copy is not null)
			{
				copy.Transform = here;
				target.AddChild(copy);
				if (area > biggestArea)
				{
					biggestArea = area;
					biggest = copy;
				}
			}

			foreach (Node child in current.GetChildren())
				Walk(child, here);
		}

		// The root's own transform (its position in whatever scene placed it) is irrelevant here.
		foreach (Node child in node.GetChildren())
			Walk(child, parentTransform);
		return biggest;
	}

	private void PlayIdle(AnimatedSprite2D sprite)
	{
		SpriteFrames frames = sprite.SpriteFrames;
		string chosen = null;
		if (frames.HasAnimation(_animation))
			chosen = _animation;
		else
		{
			foreach (string candidate in IdleFallbacks)
			{
				if (frames.HasAnimation(candidate))
				{
					chosen = candidate;
					break;
				}
			}
		}
		// Any "...idle..." variant (digging_idle, idle_closed...), then flying (a flyer's idle),
		// then plain locomotion, then whatever is first.
		string[] names = frames.GetAnimationNames();
		chosen ??= System.Array.Find(names, n => n.ToLowerInvariant().Contains("idle"));
		foreach (string fallback in new[] { "fly", "flight", "flying", "move", "walk", "run" })
			chosen ??= System.Array.Find(names, n => n.ToLowerInvariant() == fallback);
		chosen ??= names.Length > 0 ? names[0] : null;
		if (chosen is null)
			return;
		sprite.Play(chosen);
		// Start each figure at a random point of its loop so a row of them doesn't breathe in sync.
		int count = frames.GetFrameCount(chosen);
		if (count > 1)
			sprite.Frame = (int)(GD.Randi() % (uint)count);
	}

	private static Texture2D FirstFrame(SpriteFrames frames, StringName animation)
	{
		if (frames.HasAnimation(animation) && frames.GetFrameCount(animation) > 0)
			return frames.GetFrameTexture(animation, 0);
		string[] names = frames.GetAnimationNames();
		return names.Length > 0 && frames.GetFrameCount(names[0]) > 0 ? frames.GetFrameTexture(names[0], 0) : null;
	}

	// Opaque-pixel bounds of the main sprite (union over up to 12 idle frames), in figure space.
	private static Rect2 MeasureContent(Node2D main)
	{
		Rect2? used = null;
		Vector2 frameSize = Vector2.Zero;
		bool centered = true;
		Vector2 offset = Vector2.Zero;
		bool flipH = false;

		if (main is AnimatedSprite2D animated)
		{
			centered = animated.Centered;
			offset = animated.Offset;
			flipH = animated.FlipH;
			StringName anim = animated.Animation;
			int count = Mathf.Min(animated.SpriteFrames.GetFrameCount(anim), 12);
			for (int i = 0; i < count; i++)
			{
				Texture2D tex = animated.SpriteFrames.GetFrameTexture(anim, i);
				if (tex is null)
					continue;
				frameSize = tex.GetSize();
				Rect2? r = UsedRect(tex.GetImage());
				if (r is not null)
					used = used is null ? r : used.Value.Merge(r.Value);
			}
		}
		else if (main is Sprite2D sprite)
		{
			centered = sprite.Centered;
			offset = sprite.Offset;
			flipH = sprite.FlipH;
			Image image = sprite.Texture.GetImage();
			Rect2I region = sprite.RegionEnabled
				? (Rect2I)sprite.RegionRect
				: FrameRegion(sprite);
			frameSize = region.Size;
			if (image is not null)
				used = UsedRect(image.GetRegion(region));
		}

		if (frameSize == Vector2.Zero)
			return new Rect2(main.Position, Vector2.One);
		Rect2 pixels = used ?? new Rect2(Vector2.Zero, frameSize);
		if (flipH)
			pixels.Position = new Vector2(frameSize.X - pixels.End.X, pixels.Position.Y);

		// Pixel rect → sprite-local → figure space (through the copied sprite's transform).
		Vector2 origin = (centered ? -frameSize * 0.5f : Vector2.Zero) + offset;
		Vector2 a = main.Transform * (origin + pixels.Position);
		Vector2 b = main.Transform * (origin + pixels.End);
		return new Rect2(new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), (b - a).Abs());
	}

	private static Rect2I FrameRegion(Sprite2D sprite)
	{
		Vector2I size = new(sprite.Texture.GetWidth() / Mathf.Max(1, sprite.Hframes), sprite.Texture.GetHeight() / Mathf.Max(1, sprite.Vframes));
		Vector2I cell = new(sprite.Frame % Mathf.Max(1, sprite.Hframes), sprite.Frame / Mathf.Max(1, sprite.Hframes));
		return new Rect2I(cell * size, size);
	}

	private static Rect2? UsedRect(Image image)
	{
		if (image is null || image.IsEmpty())
			return null;
		if (image.IsCompressed())
			image.Decompress();
		Rect2I r = image.GetUsedRect();
		return r.Size == Vector2I.Zero ? null : new Rect2(r.Position, r.Size);
	}
}
