extends BTAction

func _tick(_delta: float) -> Status:
	if not agent.isShooting:
		agent.call("ToggleShoot")
	return SUCCESS
