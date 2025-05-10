using Godot;
using System;
using System.Diagnostics;
using System.Reflection.Metadata;

public partial class Player : CharacterBody3D
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
	private float yaw = 0f;
	private float pitch = 0f;
	private Node3D PCamera;
	private RayCast3D rayCast;
	private Node3D eyes;

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
	private MeshInstance3D debugLineMesh;
	private ImmediateMesh rayMesh;
	private MeshInstance3D hitMarkerMesh;


	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//animationPlayer.AnimationFinished += (animationName) => Fall(animationName);
		animationTree = GetNode<AnimationTree>("AnimationTree");
		PCamera = GetNode<Node3D>("%PhantomCamera3D");
		cameraOrbit = GetNode<Node3D>("CameraOrbit");
		eyes = GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<Node3D>("Eyes");
		collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		capsuleShape = collisionShape.Shape as CapsuleShape3D;
		despawnTimer = GetNode<Timer>("DespawnTimer");
		rayCast = GetNode<Node3D>("%pistol").GetNode<RayCast3D>("RayCast3D");
		debugNode = GetNode<Node3D>("%WorldDebugLines");

		AddVisibleRaycast();
	}

	public void AddVisibleRaycast()
	{
		debugLineMesh = new MeshInstance3D();
		rayMesh = new ImmediateMesh();

		debugLineMesh.Mesh = rayMesh;
		debugLineMesh.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0, 1, 0),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		debugNode.AddChild(debugLineMesh);
		debugLineMesh.GlobalTransform = Transform3D.Identity;

		hitMarkerMesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.1f) },
			Visible = false
		};
		StandardMaterial3D markerMat = new StandardMaterial3D
		{
			AlbedoColor = new Color(1, 0, 0),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		hitMarkerMesh.MaterialOverride = markerMat;
		debugNode.AddChild(hitMarkerMesh);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (isDead)
		{
			if (despawnTimer.IsStopped()) QueueFree();
			Velocity = Vector3.Zero;
			HandleAnimations();
			MoveAndSlide();
			return;
		}
		Vector3 velocity = Velocity;
		velocity = HandleMovement(velocity);
		velocity = HandleJump(velocity, delta);
		Velocity = velocity;
		MoveAndSlide();
		HandleAnimations();
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
		else
		{
			airTime = 0;
			if (currentAnim == CurrentAnim.FallA)
			    currentAnim = CurrentAnim.PistolIdleA;
			animationTree.Set("parameters/AimFall/blend_amount", 1);
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
			animationTree.Set("parameters/jump/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}
		return velocity;
	}

	public Vector3 HandleMovement(Vector3 velocity)
	{
		if (isDead || currentAnim == CurrentAnim.DieA)
			return velocity;
		if (currentAnim == CurrentAnim.FallA)
			return velocity;

		Vector2 inputDir = Input.GetVector("moveRight", "moveLeft", "moveBackward", "moveForward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

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

	public void HandleAnimations()
	{
		if (isDead)
		{
			animationTree.Set("parameters/AimFall/blend_amount", 0);
			animationTree.Set("parameters/Movement/transition_request", "Die");
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
			case CurrentAnim.DieA:
				animationTree.Set("parameters/AimFall/blend_amount", 0);
				animationTree.Set("parameters/Movement/transition_request", "Die");
				break;
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
			Speed = 5f;
			capsuleShape.Height = 2f;
			collisionShape.Position = new Vector3(0, 0.875f, 0);
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
			eyes.Position = new Vector3(-0.039f, 1.636f, 0.149f);
			animationTree.Set("parameters/CrouchWalk/blend_amount", 0);
			animationTree.Set("parameters/CrouchIdle/blend_amount", 0);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (isDead) return;
		if (@event is InputEventMouseMotion mouseMotion)
		{
			HandleMouseMovement(mouseMotion);
		}

		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			HandleMouseButtons(mouseButtonEvent);
		}

		if (@event is InputEventKey keyEvent)
		{
			if (keyEvent.IsActionPressed("ReloadLevel")) // R key
				GetTree().ReloadCurrentScene();

			HandleCrouching(keyEvent);
		}
	}

	public void HandleMouseMovement(InputEventMouseMotion mouseMotion)
	{
		yaw -= mouseMotion.Relative.X * YawSensitivity;
		pitch -= mouseMotion.Relative.Y * PitchSensitivity;

		pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
		yaw = yaw % 360;

		Vector3 cameraRotation = new Vector3(pitch, yaw + 180, 0);

		PCamera.Call("set_third_person_rotation_degrees", cameraRotation);


		float normalizedPitch = pitch / MaxPitch;
		animationTree.Set("parameters/pitch/blend_amount", normalizedPitch * 0.65f);
		RotationDegrees = new Vector3(0, yaw, 0);
	}

	public void HandleMouseButtons(InputEventMouseButton mouseButtonEvent)
	{
		if (mouseButtonEvent.IsPressed() && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			animationTree.Set("parameters/Shoot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);


			// Get the camera and direction from its forward (center of screen)
			Camera3D camera = PCamera.GetNode<Camera3D>("Camera3D");
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
			rayMesh.ClearSurfaces();
			rayMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
			rayMesh.SurfaceAddVertex(start);
			rayMesh.SurfaceAddVertex(end);
			rayMesh.SurfaceEnd();

			if (result.Count > 0)
			{
				end = (Vector3)result["position"];
				hitMarkerMesh.GlobalTransform = new Transform3D(Basis.Identity, end);
				hitMarkerMesh.Visible = true;
				CharacterBody3D collider = result["collider"].As<CharacterBody3D>();
				if (collider != null && collider.IsInGroup("Enemy"))
				{
					collider.Call("TakeDamage", 14.3f);
				}
			}
			else
				hitMarkerMesh.Visible = false;

			
		}

		if (mouseButtonEvent.IsPressed() && mouseButtonEvent.ButtonIndex == MouseButton.Right)
		{
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0.75f);
			Speed = 2f;
			PCamera.GetNode<Camera3D>("Camera3D").Fov = 45f;
		}
		else if (mouseButtonEvent.IsReleased() && mouseButtonEvent.ButtonIndex == MouseButton.Right)
		{
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
			Speed = 5f;
			PCamera.GetNode<Camera3D>("Camera3D").Fov = 70f;
		}
	}

	public void HandleCrouching(InputEventKey keyEvent)
	{
		if (keyEvent.IsActionPressed("crouch"))
			isCrouching = true;
		else if (keyEvent.IsActionReleased("crouch"))
			isCrouching = false;
	}

	public void TakeDamage(float damage)
	{
		currentHealth -= damage;
		GD.Print("Current Health: " + currentHealth);
		if (currentHealth <= 0 && !isDead)
		{
			isDead = true;
			currentHealth = 0;
			animationTree.Set("parameters/AimFall/blend_amount", 0);
			currentAnim = CurrentAnim.DieA;
			capsuleShape.Height = 0.01f;
			capsuleShape.Radius = 0.01f;
			rayMesh.ClearSurfaces();
			despawnTimer.Start();
		}
	}
	
}
