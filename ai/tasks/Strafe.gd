extends BTAction

func _tick(_delta: float) -> Status:
	var posReached = agent.call("TargetPosReachedOffset", 0.25)	
	if posReached :
		agent.call("Strafe")
	return RUNNING
