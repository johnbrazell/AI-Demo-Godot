extends BTAction

func _tick(_delta: float) -> Status:
	agent.call("RemoveFoundSound")
	return SUCCESS
