extends BTAction

func _tick(_delta: float) -> Status:
	if agent.isAiming :
		agent.call("ToggleAim")
	
	return SUCCESS
