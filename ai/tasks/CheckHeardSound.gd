extends BTAction

var heardSound = false

func reset() -> void:
	heardSound = false
	
func _tick(_delta: float) -> Status:
	heardSound = agent.call("HeardSound")
	if heardSound :
		return SUCCESS
	else : 
		return FAILURE
