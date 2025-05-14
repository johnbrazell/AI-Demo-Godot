extends BTAction

func _tick(delta: float) -> Status:
	agent.call("UpdateLookNextPathPos")
	return SUCCESS
