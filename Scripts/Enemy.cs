using Godot;
using System;
using System.Diagnostics;
using System.Reflection.Metadata;

public partial class Enemy : CharacterBody3D
{
	[Export] public float MaxHealth = 100f;
	[Export] public float currentHealth = 100f;
	[Export] public bool isDead = false;
	[Export] public Timer despawnTimer;
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

	
	private Node3D debugNode;
	private MeshInstance3D debugLineMeshRed;
	private ImmediateMesh rayMeshRed;
	private MeshInstance3D hitMarkerMeshRed;

	private CharacterBody3D player;
	private Node3D playerEyes;
	private NavigationAgent3D navAgent;
	private NavigationRegion3D navMap;
	private Vector3 patrolTarget = Vector3.Zero;


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
		despawnTimer.Timeout += () => QueueFree();
		rayCast = GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<BoneAttachment3D>("RightHandBoneAttachment3D").GetNode<Node3D>("pistol").GetNode<RayCast3D>("RayCast3D");
		debugNode = GetNode<Node3D>("%WorldDebugLines");
		navAgent = GetNode<NavigationAgent3D>("NavigationAgent3D");
		navMap = GetTree().Root.GetNode<NavigationRegion3D>("NavigationRegion3D");
		player = GetNode<Player>("%Player");
		playerEyes = player.GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<Node3D>("Eyes");
		navAgent.TargetPosition = player.GlobalPosition;

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
			AlbedoColor = new Color(0, 0, 1),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		hitMarkerMeshRed.MaterialOverride = markerMat;
		debugNode.AddChild(hitMarkerMeshRed);
	}

	public void ResetCollision()
	{
		capsuleShape.Height = 1.864f;
		capsuleShape.Radius = 0.248f;
		collisionShape.Position = new Vector3(0, 0.875f, 0);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (isDead)
		{
			Velocity = Vector3.Zero;
			MoveAndSlide();
			return;
		}
		CanSeePlayerThisFrame = CanSeePlayer();
		if (CanSeePlayerThisFrame)
			LookAt(player.GlobalPosition, Vector3.Up);

		Vector3 velocity = Velocity;
		velocity = HandleMovement(velocity);
		velocity = HandleJump(velocity, delta);
		Velocity = velocity;
		MoveAndSlide();
		HandleAnimations();
	}

	public bool CanSeePlayer()
	{
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
			//CharacterBody3D collider = result["collider"].As<CharacterBody3D>();
			if (result["collider"].Obj is Node3D collider && collider.IsInGroup("Player"))
				return true;
			return false;
		}
		else
			return false;
	}

	public void Jump()
	{
		isJumping = !isJumping;
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
			velocity += GetGravity() * (float)delta;
		}
		return velocity;
	}

	public Vector3 HandleMovement(Vector3 velocity)
	{
		if (isDead || currentAnim == CurrentAnim.DieA)
			return velocity;
		if (currentAnim == CurrentAnim.FallA)
			return velocity;
		if (navAgent == null || player == null)
			return velocity;

		if (CanSeePlayerThisFrame)
			navAgent.TargetPosition = player.GlobalPosition;
		else
		{
			if ((GlobalPosition - patrolTarget).Length() < 1f || navAgent.IsNavigationFinished())
			{
				patrolTarget = GetRandomPoint(GlobalPosition, 60);
			}
			navAgent.TargetPosition = patrolTarget;
		}

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
		if (navMap == null)
	{
		GD.PushError("navRegion is null! Assign it in the Inspector.");
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

			var navMapRid = navMap.GetNavigationMap();
			Vector3? closestPoint = NavigationServer3D.MapGetClosestPoint(navMapRid, randomPoint);
			if (closestPoint != null)
				return closestPoint.Value;
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
			Speed = 5f;
			capsuleShape.Height = 1.864f;
			collisionShape.Position = new Vector3(0, 0.875f, 0);
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
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
		{
			animationTree.Set("parameters/Shoot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);


			// Get the camera and direction from its forward (center of screen)
			//Camera3D camera = PCamera.GetNode<Camera3D>("Camera3D");
			Vector3 start = camera.GlobalPosition;
			Vector3 direction = -camera.GlobalTransform.Basis.Z;// Forward direction

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
				CharacterBody3D collider = result["collider"].As<CharacterBody3D>();
				if (collider != null && collider.IsInGroup("Player"))
				{
					collider.Call("TakeDamage", 14.3f);
				}
			}
			else
				hitMarkerMeshRed.Visible = false;
		}
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

		if (currentHealth <= 0 && !isDead)
		{
			isDead = true;
			currentHealth = 0;

			currentAnim = CurrentAnim.DieA;
			HandleAnimations();

			capsuleShape.Height = 0.01f;
			capsuleShape.Radius = 0.01f;
			rayMeshRed.ClearSurfaces();

			despawnTimer.Start();			
		}
	}
	
}
