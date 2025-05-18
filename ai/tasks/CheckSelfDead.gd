extends BTAction

var isDead = false

func reset() -> void:
	isDead = false

func _tick(_delta: float) -> Status:
	isDead = agent.call("CheckSelfDead")
	if isDead :
		return SUCCESS
	else :
		return FAILURE
	
