using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;

public partial class Enemy : CharacterBody3D
{
	[Export] public float MaxHealth = 100f;
	[Export] public float currentHealth = 100f;
	[Export] public bool isDead = false;
	[Export] public Timer despawnTimer;
	public Timer shootTimer;
	[Export] public float Speed = 5.0f;
	[Export] public float JumpVelocity = 5f;
	[Export] public AnimationPlayer animationPlayer;
	[Export] public AnimationTree animationTree;
	[Export] public float YawSensitivity = 0.1f;
	[Export] public float PitchSensitivity = 0.1f;
	[Export] public float MaxPitch = 85f;
	[Export] public float MinPitch = -85f;
	[Export] public Node3D cameraOrbit;
	[Export] public CollisionShape3D collisionShape;
	[Export] public CapsuleShape3D capsuleShape;

	private bool isCrouching = false;
	private bool isJumping = false;
	private bool isMoving = false;
	private bool isAiming = false;
	private bool isShooting = false;
	private float yaw = 0f;
	private float pitch = 0f;
	private Node3D PCamera;
	private Camera3D camera;
	private RayCast3D rayCast;
	private Node3D eyes;
	private bool CanSeePlayerThisFrame = false;

	private float sprintBlend = 0;
	private float shootBlend = 0;
	private float jumpStartBlend = 0;
	private float jumpBlend = 0;
	private float jumpLandBlend = 0;
	private float blendSpeed = 15f;
	private float airTime = 0f;
	private float fallThreshold = 1.25f;
	private enum CurrentAnim
	{
		PistolIdleA,
		WalkA,
		SprintA,
		FallA,
		DieA
	}
	private CurrentAnim currentAnim = CurrentAnim.PistolIdleA;
	private enum CurrentTeam
	{
		Blue,
		Red
	}
	private CurrentTeam currentTeam = CurrentTeam.Red;
	private PackedScene enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");


	public Node3D debugNode;
	private MeshInstance3D debugLineMeshRed;
	private ImmediateMesh rayMeshRed;
	private MeshInstance3D hitMarkerMeshRed;

	private CharacterBody3D player;
	private Node3D playerEyes;
	private NavigationAgent3D navAgent;
	private NavigationRegion3D navMap;
	private bool navMapReady = false;
	//private Vector3 patrolPoint;
	private RandomNumberGenerator rng = new();
	private bool initializedPatrol = false;
	private bool isDespawning = false;
	private AudioStreamPlayer3D audioStreamPlayer;
	private Node3D teamSoundNode;
	private Node3D EnemySoundNode;
	private MeshInstance3D soundsPosMesh;
	private Timer soundTimer;
	private Vector3 closestSoundPos = Vector3.Zero;

	private Vector3 lastCheckedPosition = Vector3.Zero;
	private float stuckTimer = 0f;
	private int stuckState = 0; // 0 = not stuck, 1 = jump attempted
	private float stuckThreshold = 0.2f; // how little distance = stuck
	private float stuckCooldown = 0.5f;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		animationPlayer.AnimationFinished += (animName) => OnAnimationFinished(animName);
		animationTree = GetNode<AnimationTree>("AnimationTree");
		cameraOrbit = GetNode<Node3D>("CameraOrbit");
		PCamera = cameraOrbit.GetNode<Node3D>("PhantomCamera3D");
		camera = PCamera.GetNode<Camera3D>("Camera3D");
		eyes = GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<Node3D>("Eyes");
		collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		capsuleShape = collisionShape.Shape as CapsuleShape3D;
		capsuleShape = capsuleShape.Duplicate() as CapsuleShape3D;
		collisionShape.Shape = capsuleShape;
		despawnTimer = GetNode<Timer>("DespawnTimer");
		despawnTimer.Timeout += () => Respawn();
		shootTimer = GetNode<Timer>("ShootTimer");
		shootTimer.Timeout += Shoot;
		rayCast = GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<BoneAttachment3D>("RightHandBoneAttachment3D").GetNode<Node3D>("pistol").GetNode<RayCast3D>("RayCast3D");
		debugNode = GetParent<Node3D>().GetNode<Node3D>("%WorldDebugLines");
		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		navMap = GetParent<Node3D>().GetNode<NavigationRegion3D>("%NavigationRegion3D");
		NavigationServer3D.MapChanged += (rid) => { navMapReady = true; };
		audioStreamPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
		soundTimer = GetNode<Timer>("SoundTimer");
		soundTimer.Timeout += () => RemoveSound();

		if (currentTeam == CurrentTeam.Red)
		{
			teamSoundNode = debugNode.GetNode<Node3D>("RedSounds");
			EnemySoundNode = debugNode.GetNode<Node3D>("BlueSounds");
		}
		else
		{
			teamSoundNode = debugNode.GetNode<Node3D>("BlueSounds");
			EnemySoundNode = debugNode.GetNode<Node3D>("RedSounds");
		}

		AddVisibleRaycast();
		ResetCollision();
	}

	public void AddVisibleRaycast()
	{
		debugLineMeshRed = new MeshInstance3D();
		rayMeshRed = new ImmediateMesh();

		debugLineMeshRed.Mesh = rayMeshRed;
		debugLineMeshRed.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(1, 0, 0),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		debugNode.AddChild(debugLineMeshRed);
		debugLineMeshRed.GlobalTransform = Transform3D.Identity;

		hitMarkerMeshRed = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.1f) },
			Visible = false
		};
		StandardMaterial3D markerMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(0, 1, 0),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		hitMarkerMeshRed.MaterialOverride = markerMat;
		debugNode.AddChild(hitMarkerMeshRed);

		soundsPosMesh = new MeshInstance3D
		{
			Mesh = new SphereMesh { Radius = 0.1f, Height = 0.1f },
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(1, 0, 0),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			},
			Visible = true
		};
		teamSoundNode.AddChild(soundsPosMesh);
	}

	public void ResetCollision()
	{
		Speed = 5f;
		capsuleShape.Height = 2f;
		collisionShape.Position = new Vector3(0, 0.875f, 0);
		cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
		eyes.Position = new Vector3(-0.039f, 1.636f, 0.149f);
		isAiming = false;
    	PCamera.GetNode<Camera3D>("Camera3D").Fov = 70f;
	}

	public void SetTargetNavPos(Node3D newTarget = null)
	{
		if (newTarget != null)
		{
			navAgent.TargetPosition = newTarget.GlobalPosition;
			return;
		}
		else
		{
			navAgent.TargetPosition = GetRandomPoint(GlobalPosition, 60);
		}
		// {
		// 	var players = GetTree().GetNodesInGroup("Blue");
		// 	if (players.Count > 0)
		// 		player = players[(int)rng.RandfRange(0, players.Count - 1)] as CharacterBody3D;
		// }

			// if (player != null)
			// {
			// 	playerEyes = player.GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<Node3D>("Eyes");
			// 	if (navAgent != null && IsInsideTree())
			// 		navAgent.TargetPosition = player.GlobalPosition;
			// }
	}

	public void SetTargetNavPos(Vector3 newTarget)
	{
		if (newTarget != Vector3.Zero)
			navAgent.TargetPosition = newTarget;
	}

	public bool TargetPosReached()
	{
		if (navAgent != null)
		{
			Vector2 targetposV2 = new Vector2(navAgent.TargetPosition.X, navAgent.TargetPosition.Z);
			Vector2 currentPosV2 = new Vector2(GlobalPosition.X, GlobalPosition.Z);
			float distance = targetposV2.DistanceTo(currentPosV2);
			GD.Print(distance);
			if (distance <= 1.5f)
				return true;
		}
		
		return false;
	}

	public bool TargetPosReached(float offset)
	{
		if (navAgent != null)
		{
			Vector2 targetposV2 = new Vector2(navAgent.TargetPosition.X, navAgent.TargetPosition.Z);
			Vector2 currentPosV2 = new Vector2(GlobalPosition.X, GlobalPosition.Z);
			float distance = targetposV2.DistanceTo(currentPosV2);
			if (distance <= offset)
				return true;
		}
		return false;
	}

	public override void _PhysicsProcess(double delta)
	{
		// if (isDespawning || !GodotObject.IsInstanceValid(playerEyes))
		// return;

		if (isDead)
		{
			Velocity = Vector3.Zero;
			Velocity += GetGravity() * (float)delta;
			MoveAndSlide();
			return;
		}

		if (navMapReady && HeardSound())
		{
			Vector3 closestSoundPos = GetClosestSoundPos();
			if (closestSoundPos != GlobalPosition)
			{
				SetTargetNavPos(closestSoundPos);
				UpdateLook(closestSoundPos);
			}
			if (TargetPosReached())
				RemoveFoundSound();
		}

		if (!TargetPosReached())
		{
			stuckTimer += (float)delta;
			UpdateLook(navAgent.GetNextPathPosition() + Vector3.Up);
			if (stuckTimer >= stuckCooldown && GlobalPosition.DistanceTo(lastCheckedPosition) <= stuckThreshold)
				HandleStuck();
		}
		else
			SetTargetNavPos();
		

		Vector3 velocity = Velocity;
		velocity = HandleMovement(velocity);
		velocity = HandleJump(velocity, delta);
		Velocity = velocity;
		MoveAndSlide();
		HandleAnimations();
	}

	public void HandleStuck()
	{
		if (stuckState == 0 && IsOnFloor())
		{
			Jump();
			stuckState = 1;
		}
		else
		{
			Vector3 reroute = GetRandomPoint(navAgent.TargetPosition, 10);
			if (reroute.IsFinite())
				navAgent.TargetPosition = reroute;
			else
				navAgent.TargetPosition = GlobalPosition;
			stuckState = 0;
		}

		lastCheckedPosition = GlobalPosition;
		stuckTimer = 0f;
	}

	private void UpdateLook(Vector3 targetGPos)
	{
		Vector3 toTarget = targetGPos - eyes.GlobalPosition;
		float verticalDistance = toTarget.Y;
		float horizontalDistance = new Vector2(toTarget.X, toTarget.Z).Length();

		if (horizontalDistance == 0)
			return;

		float pitchRadians = Mathf.Atan2(verticalDistance, horizontalDistance);
		float pitchDegrees = Mathf.RadToDeg(pitchRadians);

		// Clamp the angle between MinPitch and MaxPitch
		pitchDegrees = Mathf.Clamp(pitchDegrees, MinPitch, MaxPitch);

		// Convert to Blend3 range: -1 (down) to 0 (neutral) to 1 (up)
		float normalizedBlend = Mathf.Remap(pitchDegrees, MinPitch, MaxPitch, -1f, 1f);

		animationTree.Set("parameters/pitch/blend_amount", normalizedBlend);

		// Calculate yaw angle to look at the player
		float targetYaw = Mathf.Atan2(toTarget.X, toTarget.Z);
		float currentYaw = Rotation.Y;

		// Smoothly interpolate yaw
		float newYaw = Mathf.LerpAngle(currentYaw, targetYaw, 0.1f); // Adjust 0.1f for turn speed

		// Apply rotation (only yaw changes)
		Rotation = new Vector3(Rotation.X, newYaw, Rotation.Z);
	}

	public bool CanSeePlayer()
	{
		if (isDespawning || !GodotObject.IsInstanceValid(playerEyes))
		return false;

		Vector3 start = eyes.GlobalPosition;
		Vector3 direction = (playerEyes.GlobalPosition - start).Normalized();

		// Cast the ray
		float rayLength = 1000f;
		Vector3 end = start + direction * rayLength;

		// Optional: Physics raycast
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(start, end);
		query.Exclude = new Godot.Collections.Array<Rid> { this.GetRid() };
		var result = spaceState.IntersectRay(query);

		// Draw using ImmediateMesh
		// rayMeshRed.ClearSurfaces();
		// rayMeshRed.SurfaceBegin(Mesh.PrimitiveType.Lines);
		// rayMeshRed.SurfaceAddVertex(start);
		// rayMeshRed.SurfaceAddVertex(end);
		// rayMeshRed.SurfaceEnd();

		if (result.Count > 0)
		{
			// end = (Vector3)result["position"];
			// hitMarkerMeshRed.GlobalTransform = new Transform3D(Basis.Identity, end);
			// hitMarkerMeshRed.Visible = true;
			string enemyTeam;
			if (currentTeam == CurrentTeam.Blue)
			{
				enemyTeam = CurrentTeam.Red.ToString();
			}
			else
			{
				enemyTeam = CurrentTeam.Blue.ToString();
			}
			if (result["collider"].Obj is Node3D collider && collider.IsInGroup(enemyTeam))
				return true;
			return false;
		}
		else
			return false;
	}

	public void Jump()
	{
		isJumping = !isJumping;
		GD.Print("Jumping: " + isJumping);
	}

	public Vector3 HandleJump(Vector3 velocity, double delta)
	{
		if (!IsOnFloor())
		{
			airTime += (float)delta;
			velocity += GetGravity() * (float)delta;

			if (airTime >= fallThreshold && currentAnim != CurrentAnim.FallA)
			{
				currentAnim = CurrentAnim.FallA;
				animationTree.Set("parameters/AimFall/blend_amount", 0);
			}
		}
		else if (isJumping && IsOnFloor())
		{
			airTime = 0;
			if (currentAnim == CurrentAnim.FallA)
				currentAnim = CurrentAnim.PistolIdleA;
			animationTree.Set("parameters/AimFall/blend_amount", 1);
			velocity.Y = JumpVelocity;
			animationTree.Set("parameters/jump/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			isJumping = false;
		}
		else if (IsOnFloor())
		{
			airTime = 0;
			isJumping = false;
			velocity += GetGravity() * (float)delta;
		}
		velocity.Y = Mathf.Clamp(velocity.Y, -100f, JumpVelocity);
		return velocity;
	}

	public Vector3 HandleMovement(Vector3 velocity)
	{
		if (isDead || currentAnim == CurrentAnim.DieA)
			return velocity;
		if (currentAnim == CurrentAnim.FallA)
			return velocity;
		if (navAgent == null)
			return velocity;

		Vector3 direction = (navAgent.GetNextPathPosition() - GlobalPosition).Normalized();
		Vector3 desiredVelocity = new Vector3(direction.X, Velocity.Y, direction.Z) * Speed;
		velocity = desiredVelocity;

		if (!direction.IsZeroApprox())
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
			isMoving = true;

			if (new Vector2(velocity.X, velocity.Z).Length() > 4f)
				currentAnim = CurrentAnim.SprintA;
			else
				currentAnim = CurrentAnim.WalkA;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
			currentAnim = CurrentAnim.PistolIdleA;
			isMoving = false;
		}
		return velocity;
	}

	public Vector3 GetRandomPoint(Vector3 center, float radius)
	{
		var navMapRid = navMap.GetNavigationMap();
		if (NavigationServer3D.MapGetIterationId(navMapRid) == 0)
		{
			GD.Print("Navigation map not ready, skipping GetRandomPoint().");
			return center;
		}

		for (int i = 0; i < 20; i++)
		{
			Vector3 randomOffset = new Vector3(
				GD.Randf() * 2f - 1f,
				0,
				GD.Randf() * 2f - 1f
				).Normalized() * (float)(GD.Randf() * radius);
			Vector3 randomPoint = center + randomOffset;

			Vector3 closestPoint = center;
			closestPoint = NavigationServer3D.MapGetClosestPoint(navMapRid, randomPoint);
			if (closestPoint != center && closestPoint.Y <= 9f && closestPoint.IsFinite())
				return closestPoint;
		}
		return center;
	}

	public void HandleAnimations()
	{
		if (isDead)
		{
			animationTree.Set("parameters/AimFall/blend_amount", 0);
			animationTree.Active = false;
			animationPlayer.Play("EnemyAnimations/Death01");
			return;
		}

		switch (currentAnim)
		{
			case CurrentAnim.PistolIdleA:
				animationTree.Set("parameters/Movement/transition_request", "Idle");
				break;
			case CurrentAnim.SprintA:
				animationTree.Set("parameters/Movement/transition_request", "Sprint");
				break;
			case CurrentAnim.WalkA:
				animationTree.Set("parameters/Movement/transition_request", "Walk");
				break;
			case CurrentAnim.FallA:
				animationTree.Set("parameters/Movement/transition_request", "Fall");
				break;
			// case CurrentAnim.DieA:
			// 	animationTree.Set("parameters/AimFall/blend_amount", 0);
			// 	animationTree.Set("parameters/die/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
			// 	break;
			default:
				animationTree.Set("parameters/Movement/transition_request", "Idle");
				break;
		}

		if (isCrouching)
		{
			Speed = 2f;
			capsuleShape.Height = 1.35f;
			collisionShape.Position = new Vector3(0.1f, 0.5f, 0);
			cameraOrbit.Position = new Vector3(-0.5f, 1.35f, 0.5f);
			eyes.Position = new Vector3(0.087f, 0.93f, 0.261f);

			if (isMoving)
			{
				animationTree.Set("parameters/CrouchWalk/blend_amount", 1);
				animationTree.Set("parameters/CrouchIdle/blend_amount", 0);
			}
			else
			{
				animationTree.Set("parameters/CrouchWalk/blend_amount", 0);
				animationTree.Set("parameters/CrouchIdle/blend_amount", 1);
			}
		}
		else
		{
			ResetCollision();
			animationTree.Set("parameters/CrouchWalk/blend_amount", 0);
			animationTree.Set("parameters/CrouchIdle/blend_amount", 0);
		}
	}

	public void OnAnimationFinished(String animationName)
	{
		if (animationName == "EnemyAnimations/Death01")
		{
			animationPlayer.Pause();
		}
	}

	public void ToggleShoot()
	{
		isShooting = !isShooting;

		if (isShooting)
			ResetShootTimer();
		else
			shootTimer.Stop();
	}

	public void ResetShootTimer()
	{
		float delay = rng.RandfRange(0.075f, 0.75f);
		shootTimer.WaitTime = delay;
		shootTimer.Start();
	}

	public void Shoot()
	{
		if (!isShooting || !GodotObject.IsInstanceValid(playerEyes))
			return;

		SpawnSound();
		animationTree.Set("parameters/Shoot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);

		// Get the camera and direction from its forward (center of screen)
		//Camera3D camera = PCamera.GetNode<Camera3D>("Camera3D");
		Vector3 start = rayCast.GlobalPosition;
		Vector3 direction = (playerEyes.GlobalPosition - start).Normalized();//-camera.GlobalTransform.Basis.Z;// Forward direction

		// Cast the ray
		float rayLength = 1000f;
		Vector3 end = start + direction * rayLength;

		// Optional: Physics raycast
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(start, end);
		var result = spaceState.IntersectRay(query);

		// Draw using ImmediateMesh
		rayMeshRed.ClearSurfaces();
		rayMeshRed.SurfaceBegin(Mesh.PrimitiveType.Lines);
		rayMeshRed.SurfaceAddVertex(start);
		rayMeshRed.SurfaceAddVertex(end);
		rayMeshRed.SurfaceEnd();

		if (result.Count > 0)
		{
			end = (Vector3)result["position"];
			hitMarkerMeshRed.GlobalTransform = new Transform3D(Basis.Identity, end);
			hitMarkerMeshRed.Visible = true;
			Node collider = result["collider"].As<Node>();

			string enemyTeam;
			if (currentTeam == CurrentTeam.Blue)
			{
				enemyTeam = CurrentTeam.Red.ToString();
			}
			else
			{
				enemyTeam = CurrentTeam.Blue.ToString();
			}

			if (collider is CharacterBody3D character && character.IsInGroup(enemyTeam))
			{
				collider.Call("TakeDamage", 14.3f);
			}
		}
		else
			hitMarkerMeshRed.Visible = false;

		ResetShootTimer();
	}

	public void SpawnSound()
	{
		audioStreamPlayer.Play();
		if (soundsPosMesh != null)
		{
			soundsPosMesh.Visible = false;
		}
		if (soundsPosMesh.GetParent() != teamSoundNode)
		{
			teamSoundNode.AddChild(soundsPosMesh);
		}
		soundsPosMesh.GlobalTransform = new Transform3D(Basis.Identity, rayCast.GlobalPosition);
		soundsPosMesh.Visible = true;
		soundTimer.Start();
	}

	public void RemoveSound()
	{
		if (soundsPosMesh != null && soundsPosMesh.GetParent() == teamSoundNode)
		{
			soundsPosMesh.Visible = false;
			teamSoundNode.RemoveChild(soundsPosMesh);
		}
	}

	public void RemoveFoundSound()
	{
		if (EnemySoundNode.GetChildCount() > 0)
		{
			foreach (Node3D sound in EnemySoundNode.GetChildren())
			{
				if (sound is MeshInstance3D soundMesh && sound.GlobalPosition == closestSoundPos)
				{
					sound.Visible = false;
					EnemySoundNode.RemoveChild(sound);
				}
			}
		}
	}


	public bool HeardSound()
	{
		if (EnemySoundNode.GetChildCount() > 0)
		{
			return true;
		}
		return false;
	}

	public Vector3 GetClosestSoundPos()
	{
		closestSoundPos = GlobalPosition;
		float closestDistance = 100f;

		foreach (Node3D sound in EnemySoundNode.GetChildren())
		{
			if (sound is MeshInstance3D soundMesh)
			{
				float distance = GlobalPosition.DistanceTo(soundMesh.GlobalPosition);
				if (distance < closestDistance)
				{
					closestDistance = distance;
					closestSoundPos = soundMesh.GlobalPosition;
				}
			}
		}
		return closestSoundPos;
	}

	public void ToggleAim()
	{
		isAiming = !isAiming;
		if (isAiming)
		{
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0.75f);
			Speed = 2f;
			camera.Fov = 45f;
		}
		else
		{
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
			Speed = 5f;
			camera.Fov = 70f;
		}
	}

	public void ToggleCrouch()
	{
		isCrouching = !isCrouching;
	}

	public void TakeDamage(float damage)
	{
		currentHealth -= damage;

		if (currentHealth > 100)
			currentHealth = MaxHealth;
		GD.Print("Current Health: " + Name + currentHealth);
		if (currentHealth <= 0 && !isDead)
		{
			isDead = true;
			currentHealth = 0;

			currentAnim = CurrentAnim.DieA;
			HandleAnimations();

			capsuleShape.Height = 0.01f;
			capsuleShape.Radius = 0.01f;
			rayMeshRed.ClearSurfaces();
			isShooting = false;

			despawnTimer.Start();
		}
	}

	public void Respawn()
	{
		int targetIndex = (int)rng.RandfRange(0, 5);
		List<Node3D> teamSpawns = new List<Node3D>();

		if (currentTeam == CurrentTeam.Blue)
		{
			teamSpawns.Clear();
			foreach (var spawn in navMap.GetNode<Node3D>("Level").GetNode<Node3D>("%BlueSpawns").GetChildren())
			{
				teamSpawns.Add(spawn as Node3D);
			}
		}
		else
		{
			teamSpawns.Clear();
			foreach (var spawn in navMap.GetNode<Node3D>("Level").GetNode<Node3D>("%RedSpawns").GetChildren())
			{
				teamSpawns.Add(spawn as Node3D);
			}
		}

		if (targetIndex >= teamSpawns.Count)
				targetIndex = 0; // fallback

		var spawnPoint = teamSpawns[targetIndex];
		var spawnPoint3D = spawnPoint as Node3D;
		var spawningEnemy = enemyScene.Instantiate<Enemy>();

		GetParent<Node3D>().AddChild(spawningEnemy);
		spawningEnemy.GlobalPosition = spawnPoint3D.GlobalPosition + (Vector3.Up - new Vector3(0, 0.75f, 0));
		spawningEnemy.AddToGroup("Red");

		isDespawning = true;
		player = null;
		playerEyes = null;
		isShooting = false;
		shootTimer.Stop();
		soundTimer.Stop();
		RemoveSound();
		QueueFree();

	}
	
}
