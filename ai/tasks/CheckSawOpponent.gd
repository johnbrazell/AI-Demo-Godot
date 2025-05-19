extends BTAction

func _tick(_delta: float) -> Status:
	var sawOpponent = false
	agent.call("CanSeeOpponent")
	if agent.closestOpponentPos != Vector3.ZERO:
		sawOpponent = true
		return SUCCESS
	else : 
		sawOpponent = false
		return FAILURE
