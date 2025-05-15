extends BTAction

func _tick(_delta: float) -> Status:
	if agent.closestOpponentPos != Vector3.ZERO :
		agent.call("SetLastOpponentNavPos")
		return SUCCESS
	else : 
		return FAILURE
