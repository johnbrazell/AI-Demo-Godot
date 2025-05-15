extends BTAction

func _tick(_delta: float) -> Status:
	var closestSoundPos = agent.call("GetClosestSoundPos")
	agent.call("UpdateLook", closestSoundPos)
	return SUCCESS
