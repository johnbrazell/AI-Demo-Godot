extends BTAction

func _tick(_delta: float) -> Status:
	if agent.isShooting :
		agent.call("ToggleShoot")
	
	return SUCCESS
