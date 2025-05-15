extends BTAction

func _tick(_delta: float) -> Status:
	var canSeeOpponent = agent.call("CanSeeOpponent")
	if  canSeeOpponent and is_instance_valid(canSeeOpponent):
		return SUCCESS
	else : 
		return FAILURE
