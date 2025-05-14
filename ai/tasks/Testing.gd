extends BTAction

func _tick(delta: float) -> Status:
	agent.call("Jump")
	return SUCCESS
