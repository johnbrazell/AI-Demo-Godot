extends BTAction

func _tick(_delta: float) -> Status:
	var navMap = agent.navMap
	var navMapRid = navMap.get_navigation_map()
	if NavigationServer3D.map_get_iteration_id(navMapRid) != 0 :
		agent.call("SetCoverPos")
		return SUCCESS
	else :
		return RUNNING
