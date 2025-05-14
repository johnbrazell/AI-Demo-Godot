extends BTAction

var atTarget = false

func reset() -> void:
	atTarget = false
	
func _tick(delta: float) -> Status:
	atTarget = agent.call("TargetPosReached")
	
	if atTarget:
		return SUCCESS
		
	return RUNNING
