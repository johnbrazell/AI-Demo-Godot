extends BTAction

func _tick(delta: float) -> Status:
	agent.call("HandleStuck", delta)
	return RUNNING
