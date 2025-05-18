extends BTAction

func _tick(_delta: float) -> Status:
	agent.call("SetCoverPos")
	return SUCCESS
