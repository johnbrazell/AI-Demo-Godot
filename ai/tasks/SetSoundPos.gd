extends BTAction

func _tick(_delta: float) -> Status:
	var heardSound = agent.call("HeardSound")
	if heardSound :
		var closestSoundPos = agent.call("GetClosestSoundPos")
		agent.call("SetTargetNavPosVector3", closestSoundPos)
		return SUCCESS
	else :
		return FAILURE
