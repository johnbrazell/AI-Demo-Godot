extends BTAction

func _tick(_delta: float) -> Status:
	if agent.isCrouching :
		agent.call("ToggleCrouch")
	
	return SUCCESS
