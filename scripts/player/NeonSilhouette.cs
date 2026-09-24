using Godot;

namespace Metroidvania.Player;

// Solid neon silhouette material: the sprite's alpha filled with a flat tint, additive. Tinting the
// sprite's own colours instead leaves the mostly dark character nearly invisible. Shared by the dash
// afterimages and the shadow clone so they read as the same magic.
public static class NeonSilhouette
{
	private static readonly System.Lazy<Shader> Shader = new(() => new Shader
	{
		Code = """
			shader_type canvas_item;
			render_mode blend_add;
			uniform vec4 tint = vec4(0.15, 0.65, 1.0, 1.0);
			uniform float intensity = 1.8;
			void fragment() {
				float a = texture(TEXTURE, UV).a;
				COLOR = vec4(tint.rgb * intensity, a * COLOR.a);
			}
			""",
	});

	public static ShaderMaterial CreateMaterial(Color tint, float intensity)
	{
		var material = new ShaderMaterial { Shader = Shader.Value };
		material.SetShaderParameter("tint", tint);
		material.SetShaderParameter("intensity", intensity);
		return material;
	}
}
