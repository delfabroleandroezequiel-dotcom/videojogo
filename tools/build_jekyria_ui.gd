# Builds the Jekyria UI resources from the art sheets (run headless, re-run whenever the art changes):
#   godot --headless --path . -s res://tools/build_jekyria_ui.gd
# - resources/ui/JekyriaFont.res: bitmap font from assets/sprites/deco/tipografia.png (uppercase
#   glyphs; lowercase and accented letters reuse them).
# - resources/ui/JekyriaMenuTheme.res: MainTheme + Jekyria buttons (HUDChat.png button frames) and
#   the Jekyria font for buttons.
# All rectangles are measured from the sheets (alpha scans), not guessed.
extends SceneTree

const FONT_SHEET := "res://assets/sprites/deco/tipografia.png"
const UI_SHEET := "res://assets/sprites/deco/HUDChat.png"
const FONT_OUT := "res://resources/ui/JekyriaFont.res"
const THEME_IN := "res://resources/ui/MainTheme.tres"
const THEME_OUT := "res://resources/ui/JekyriaMenuTheme.res"

const FONT_SCALE := 0.17
const GLYPH_SPACING := 4.0
const SPACE_ADVANCE := 60.0

# Rows: [top, bottom, baseline] in sheet pixels.
const ROWS := [[0, 292, 263], [292, 515, 500], [515, 724, 694]]
# Glyphs: [char, x0, x1, row, optional custom top].
const GLYPHS := [
	["A", 22, 207, 0], ["B", 211, 359, 0], ["C", 369, 519, 0], ["D", 529, 690, 0], ["E", 694, 831, 0],
	["F", 847, 979, 0], ["G", 989, 1154, 0], ["H", 1162, 1343, 0], ["I", 1360, 1440, 0], ["J", 1455, 1585, 0],
	["K", 1605, 1769, 0], ["L", 1777, 1909, 0], ["M", 1914, 2147, 0],
	["N", 22, 185, 1], ["O", 190, 342, 1], ["P", 349, 478, 1], ["Q", 486, 647, 1], ["R", 650, 823, 1],
	["S", 824, 948, 1], ["T", 964, 1111, 1], ["U", 1127, 1282, 1], ["V", 1293, 1459, 1], ["W", 1463, 1664, 1],
	["X", 1665, 1841, 1], ["Y", 1843, 1992, 1], ["Z", 1993, 2150, 1],
	["0", 30, 161, 2], ["1", 176, 247, 2], ["2", 263, 385, 2], ["3", 401, 510, 2], ["4", 528, 657, 2],
	["5", 670, 784, 2], ["6", 799, 932, 2], ["7", 951, 1076, 2], ["8", 1078, 1206, 2], ["9", 1220, 1338, 2],
	["!", 1360, 1410, 2], ["?", 1433, 1524, 2], [".", 1536, 1579, 2], [";", 1605, 1646, 2],
	[",", 1676, 1718, 2, 610], ["-", 1745, 1806, 2], ["_", 1808, 1933, 2], ["*", 1946, 2039, 2], ["+", 2045, 2150, 2],
]
# Characters drawn with another glyph (no lowercase/accents on the sheet).
const ALIASES := {"Á": "A", "À": "A", "Â": "A", "Ã": "A", "É": "E", "Ê": "E", "Í": "I", "Ó": "O", "Ô": "O", "Õ": "O", "Ú": "U", "Ü": "U", "Ñ": "N", "Ç": "C", "¿": "?", "¡": "!", ":": ";", "—": "-", "–": "-"}

# HUDChat button frames: [y0, y1] rows at x 489..844 (dark, gold, green, red).
const BUTTON_X := [489, 845]
const BUTTONS := {"dark": [70, 157], "gold": [164, 251], "green": [256, 353], "red": [356, 441]}
const BUTTON_HEIGHT := 52.0

func _initialize() -> void:
	var font := build_font()
	ResourceSaver.save(font, FONT_OUT)
	# Reload from disk so the theme references the font file instead of embedding a copy.
	font = load(FONT_OUT)
	var theme := build_theme(font)
	ResourceSaver.save(theme, THEME_OUT)
	print("Jekyria UI built: ", FONT_OUT, ", ", THEME_OUT)
	quit()

func load_image(path: String) -> Image:
	var img: Image = (load(path) as Texture2D).get_image()
	if img.is_compressed():
		img.decompress()
	return img

func build_font() -> FontFile:
	var img := load_image(FONT_SHEET)
	img.resize(int(img.get_width() * FONT_SCALE), int(img.get_height() * FONT_SCALE), Image.INTERPOLATE_LANCZOS)
	var asc := 0.0
	var desc := 0.0
	for row in ROWS:
		asc = max(asc, (row[2] - row[0]) * FONT_SCALE)
		desc = max(desc, (row[1] - row[2]) * FONT_SCALE)
	var fs := int(round(asc + desc))
	var size := Vector2i(fs, 0)

	var font := FontFile.new()
	font.fixed_size = fs
	font.fixed_size_scale_mode = TextServer.FIXED_SIZE_SCALE_ENABLED
	font.generate_mipmaps = false
	font.set_texture_image(0, size, 0, img)
	font.set_cache_ascent(0, fs, asc)
	font.set_cache_descent(0, fs, desc)

	var by_char := {}
	for g in GLYPHS:
		var row = ROWS[g[3]]
		var top: int = g[4] if g.size() > 4 else row[0]
		var rect := Rect2(g[1], top, g[2] - g[1] + 1, row[1] - top)
		by_char[g[0]] = rect
		add_glyph(font, size, g[0].unicode_at(0), rect, row[2])
		var lower: String = g[0].to_lower()
		if lower != g[0]:
			add_glyph(font, size, lower.unicode_at(0), rect, row[2])
	for alias in ALIASES:
		var target: String = ALIASES[alias]
		var g_row := row_of(target)
		for ch in [alias, alias.to_lower()]:
			add_glyph(font, size, ch.unicode_at(0), by_char[target], ROWS[g_row][2])
	font.set_glyph_advance(0, fs, 32, Vector2(SPACE_ADVANCE * FONT_SCALE, 0))
	return font

func row_of(ch: String) -> int:
	for g in GLYPHS:
		if g[0] == ch:
			return g[3]
	return 0

func add_glyph(font: FontFile, size: Vector2i, code: int, rect: Rect2, baseline: float) -> void:
	var r := Rect2(rect.position * FONT_SCALE, rect.size * FONT_SCALE)
	font.set_glyph_texture_idx(0, size, code, 0)
	font.set_glyph_uv_rect(0, size, code, r)
	font.set_glyph_size(0, size, code, r.size)
	font.set_glyph_offset(0, size, code, Vector2(0, -(baseline - rect.position.y) * FONT_SCALE))
	font.set_glyph_advance(0, size.x, code, Vector2(r.size.x + GLYPH_SPACING * FONT_SCALE, 0))

func button_style(sheet: Image, rows: Array, modulate := Color.WHITE, height := BUTTON_HEIGHT) -> StyleBoxTexture:
	var region := Rect2i(BUTTON_X[0], rows[0], BUTTON_X[1] - BUTTON_X[0], rows[1] - rows[0])
	var img := sheet.get_region(region)
	var s := height / img.get_height()
	img.resize(int(img.get_width() * s), int(height), Image.INTERPOLATE_LANCZOS)
	var style := StyleBoxTexture.new()
	style.texture = ImageTexture.create_from_image(img)
	# Pointed ends + side diamonds stay as drawn; the middle stretches (margins scale with height).
	var k := height / BUTTON_HEIGHT
	style.texture_margin_left = round(34 * k)
	style.texture_margin_right = round(34 * k)
	style.texture_margin_top = round(14 * k)
	style.texture_margin_bottom = round(14 * k)
	style.content_margin_left = round(30 * k)
	style.content_margin_right = round(30 * k)
	style.content_margin_top = round(6 * k)
	style.content_margin_bottom = round(6 * k)
	style.modulate_color = modulate
	return style

# Compact variant for buttons that sit side by side (e.g. the language row): smaller frame and text.
func add_small_button(theme: Theme, sheet: Image, font: FontFile) -> void:
	const SMALL := 34.0
	theme.set_type_variation("SmallButton", "Button")
	theme.set_stylebox("normal", "SmallButton", button_style(sheet, BUTTONS["dark"], Color.WHITE, SMALL))
	theme.set_stylebox("hover", "SmallButton", button_style(sheet, BUTTONS["gold"], Color.WHITE, SMALL))
	theme.set_stylebox("focus", "SmallButton", button_style(sheet, BUTTONS["gold"], Color.WHITE, SMALL))
	theme.set_stylebox("pressed", "SmallButton", button_style(sheet, BUTTONS["green"], Color.WHITE, SMALL))
	theme.set_stylebox("disabled", "SmallButton", button_style(sheet, BUTTONS["dark"], Color(1, 1, 1, 0.45), SMALL))
	theme.set_font("font", "SmallButton", font)
	theme.set_font_size("font_size", "SmallButton", 16)

func build_theme(font: FontFile) -> Theme:
	var theme: Theme = (load(THEME_IN) as Theme).duplicate(true)
	var sheet := load_image(UI_SHEET)
	theme.set_stylebox("normal", "Button", button_style(sheet, BUTTONS["dark"]))
	theme.set_stylebox("hover", "Button", button_style(sheet, BUTTONS["gold"]))
	theme.set_stylebox("focus", "Button", button_style(sheet, BUTTONS["gold"]))
	theme.set_stylebox("pressed", "Button", button_style(sheet, BUTTONS["green"]))
	theme.set_stylebox("disabled", "Button", button_style(sheet, BUTTONS["dark"], Color(1, 1, 1, 0.45)))
	theme.set_font("font", "Button", font)
	theme.set_font_size("font_size", "Button", 26)
	for c in ["font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color"]:
		theme.set_color(c, "Button", Color.WHITE)
	theme.set_color("font_disabled_color", "Button", Color(0.55, 0.5, 0.45, 0.8))
	theme.set_constant("outline_size", "Button", 0)
	add_small_button(theme, sheet, font)
	add_gem_checkbox(theme, sheet, font)
	return theme

# HUDChat green gem socket (measured: x 1153..1239, y 371..473).
const GEM_RECT := Rect2i(1153, 371, 87, 103)
const GEM_ICON_HEIGHT := 30.0

# CheckBox as a gem: lit green when on, dimmed (desaturated, dark) when off; no button frame, just the
# gem and gold text that brightens on hover/when checked.
func add_gem_checkbox(theme: Theme, sheet: Image, font: FontFile) -> void:
	var lit := sheet.get_region(GEM_RECT)
	var s := GEM_ICON_HEIGHT / lit.get_height()
	lit.resize(int(lit.get_width() * s), int(GEM_ICON_HEIGHT), Image.INTERPOLATE_LANCZOS)
	var dim := lit.duplicate() as Image
	for y in dim.get_height():
		for x in dim.get_width():
			var c := dim.get_pixel(x, y)
			var grey := c.get_luminance()
			dim.set_pixel(x, y, Color(grey, grey, grey, c.a).lerp(Color(0.1, 0.09, 0.08, c.a), 0.45))
	var on := ImageTexture.create_from_image(lit)
	var off := ImageTexture.create_from_image(dim)
	for icon in ["checked", "radio_checked"]:
		theme.set_icon(icon, "CheckBox", on)
	for icon in ["unchecked", "radio_unchecked"]:
		theme.set_icon(icon, "CheckBox", off)
	theme.set_icon("checked_disabled", "CheckBox", off)
	theme.set_icon("unchecked_disabled", "CheckBox", off)
	for state in ["normal", "hover", "pressed", "hover_pressed", "focus", "disabled"]:
		var empty := StyleBoxEmpty.new()
		empty.set_content_margin_all(4)
		theme.set_stylebox(state, "CheckBox", empty)
	theme.set_font("font", "CheckBox", font)
	theme.set_font_size("font_size", "CheckBox", 18)
	theme.set_color("font_color", "CheckBox", Color(0.72, 0.66, 0.56))
	for c in ["font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color"]:
		theme.set_color(c, "CheckBox", Color.WHITE)
	theme.set_constant("h_separation", "CheckBox", 8)
