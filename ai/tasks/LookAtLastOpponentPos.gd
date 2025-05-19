extends BTAction

func _tick(_delta: float) -> Status:
	agent.call("CanSeeOpponent")
	if agent.closestOpponentLastPos != Vector3.ZERO:
		agent.call("UpdateLook", agent.closestOpponentLastPos)
		return SUCCESS
	else : 
		return FAILURE
