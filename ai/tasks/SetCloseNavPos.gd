extends BTAction

func _tick(_delta: float) -> Status:
	agent.call("SetRangeNavPos", 10)
	return SUCCESS
