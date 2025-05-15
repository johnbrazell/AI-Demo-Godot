extends BTAction

var sawOpponent = false

func reset() -> void:
	sawOpponent = false
	
func _tick(_delta: float) -> Status:
	agent.call("CanSeeOpponent")
	if agent.closestOpponentPos != Vector3.ZERO:
		sawOpponent = true
		return SUCCESS
	else : 
		sawOpponent = false
		return FAILURE
