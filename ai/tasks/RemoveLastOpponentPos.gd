extends BTAction

func _tick(_delta: float) -> Status:
	if agent.closestOpponentLastPos != Vector3.ZERO :
		agent.call("RemoveLastOpponentPos")
	return SUCCESS
