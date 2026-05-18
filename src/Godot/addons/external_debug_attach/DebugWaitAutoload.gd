@tool
extends Node

## GDScript version of DebugWait Autoload
## For GDScript projects, this provides a configurable startup delay
## to allow time for the debugger to be ready

## Maximum time to wait (in seconds) when a debugger is attached
@export var max_wait_seconds: float = 2.0

var _wait_label: Label
var _elapsed_seconds: float = 0.0

func _ready() -> void:
	# Only wait if running from editor (debug mode)
	if not OS.is_debug_build():
		print("[DebugWait] Release build - skipping wait")
		return

	if not EngineDebugger.is_active():
		print("[DebugWait] No debugger attached - skipping wait (press F5 with attach to use delay)")
		return

	var skip_wait := OS.get_environment("SPECIALPG_SKIP_DEBUG_WAIT")
	if skip_wait == "1" or skip_wait.to_lower() == "true":
		print("[DebugWait] SPECIALPG_SKIP_DEBUG_WAIT set - skipping wait")
		return
	
	# Short delay so external debugger attach can catch _Ready breakpoints
	print("[DebugWait] Waiting for debugger to be ready...")
	print("[DebugWait] (Press ESC in game window to skip)")
	
	# Show a visual indicator
	_create_wait_overlay()
	_elapsed_seconds = 0.0
	
	# Use a timer instead of blocking
	set_process(true)

func _process(delta: float) -> void:
	_elapsed_seconds += delta
	var elapsed := _elapsed_seconds
	
	# Check for timeout
	if elapsed >= max_wait_seconds:
		print("[DebugWait] Wait complete - resuming game")
		_cleanup()
		return
	
	# Update the wait label
	if _wait_label:
		var remaining := max_wait_seconds - elapsed
		_wait_label.text = "Waiting for debugger... (%.1fs)\nPress ESC to skip" % remaining
	
	# Allow user to skip by pressing ESC
	if Input.is_action_just_pressed("ui_cancel"):
		print("[DebugWait] User skipped wait")
		_cleanup()

func _create_wait_overlay() -> void:
	_cleanup_overlay_only()

	# Create a simple overlay to indicate waiting
	var overlay := ColorRect.new()
	overlay.color = Color(0, 0, 0, 0.7)
	overlay.anchors_preset = Control.PRESET_FULL_RECT
	
	_wait_label = Label.new()
	_wait_label.text = "Waiting for debugger..."
	_wait_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_wait_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_wait_label.anchors_preset = Control.PRESET_FULL_RECT
	_wait_label.add_theme_color_override("font_color", Color.WHITE)
	_wait_label.add_theme_font_size_override("font_size", 24)
	
	var canvas_layer := CanvasLayer.new()
	canvas_layer.layer = 100
	canvas_layer.name = "DebugWaitOverlay"
	canvas_layer.add_child(overlay)
	canvas_layer.add_child(_wait_label)
	add_child(canvas_layer)

func _cleanup() -> void:
	set_process(false)
	_cleanup_overlay_only()
	
	print("[DebugWait] Game resumed")

func _cleanup_overlay_only() -> void:
	for child in get_children():
		if child is CanvasLayer and child.name == "DebugWaitOverlay":
			child.queue_free()
