extends Parallax2D

var fixed_y := 0.0

func _ready():
	fixed_y = global_position.y

func _process(_delta):
	global_position.y = fixed_y
