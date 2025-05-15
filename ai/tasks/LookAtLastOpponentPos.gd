extends BTAction

func _tick(_delta: float) -> Status:
	agent.call("CanSeeOpponent")
	if agent.closestOpponentPos != Vector3.ZERO:
		agent.call("UpdateLook", agent.closestOpponentPos)
		return SUCCESS
	else : 
		return FAILURE
