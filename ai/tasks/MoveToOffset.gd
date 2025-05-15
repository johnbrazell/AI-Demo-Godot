extends BTAction

var atTarget = false

func reset() -> void:
	atTarget = false
	
func _tick(_delta: float) -> Status:
	if agent.global_position.y >= 3.5 :
		atTarget = agent.call("TargetPosReachedOffset", 40)
	else :
		atTarget = agent.call("TargetPosReachedOffset", 20)
	
	if atTarget:
		return SUCCESS
	else :
		return RUNNING
