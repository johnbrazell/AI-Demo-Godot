extends BTAction

func _tick(_delta: float) -> Status:
	if not agent.isAiming:
		agent.call("ToggleAim")
	return SUCCESS
