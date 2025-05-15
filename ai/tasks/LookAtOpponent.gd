extends BTAction

func _tick(_delta: float) -> Status:
	var closestOpponent = agent.call("CanSeeOpponent")
	if closestOpponent != null and is_instance_valid(closestOpponent):
		var targetPos = closestOpponent.get("global_position")
		agent.call("UpdateLook", targetPos)
		return SUCCESS
	else : 
		return FAILURE
