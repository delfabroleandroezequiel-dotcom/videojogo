shader_type canvas_item;

// Darkens and fades a sprite toward its right and bottom edges, so a hard crop in the art doesn't
// read as a cut when it's seen from that side. Edges are in UV (0-1) so they can sit where the
// art's content actually ends, not only at the texture border (e.g. transparent padding below).
uniform float borde_derecho : hint_range(0.0, 1.0) = 1.0;
uniform float ancho_derecho : hint_range(0.0, 1.0) = 0.15;
uniform float borde_inferior : hint_range(0.0, 1.0) = 1.0;
uniform float ancho_inferior : hint_range(0.0, 1.0) = 0.15;
// How much of the colour / alpha is gone right at the edge (0 = untouched, 1 = black / invisible).
uniform float oscurecer : hint_range(0.0, 1.0) = 1.0;
uniform float desvanecer : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	// 1 inside the sprite, falling to 0 at each edge.
	float derecha = 1.0 - smoothstep(borde_derecho - ancho_derecho, borde_derecho, UV.x);
	float abajo = 1.0 - smoothstep(borde_inferior - ancho_inferior, borde_inferior, UV.y);
	float f = derecha * abajo;
	COLOR.rgb *= mix(1.0 - oscurecer, 1.0, f);
	COLOR.a *= mix(1.0 - desvanecer, 1.0, f);
}
