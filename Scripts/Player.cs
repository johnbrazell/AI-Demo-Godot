using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
	private MeshInstance3D soundsPosMesh;
	private RandomNumberGenerator rng = new();
	private enum CurrentTeam
	{
		Blue,
		Red
	}
	private CurrentTeam currentTeam = CurrentTeam.Blue;
	private CurrentTeam enemyTeam = CurrentTeam.Red;
	private PackedScene playerScene = GD.Load<PackedScene>("res://scenes/player.tscn");
	private NavigationRegion3D navMap;
	private bool isDespawning = false;
	private bool isAiming = false;
	private Camera3D camera;
	private AudioStreamPlayer3D audioStreamPlayer;
	private Timer soundTimer;
	private Node3D soundNode;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
		//animationPlayer.AnimationFinished += (animationName) => Fall(animationName);
		animationTree = GetNode<AnimationTree>("AnimationTree");
		PCamera = GetNode<Node3D>("%PhantomCamera3D");
		cameraOrbit = GetNode<Node3D>("CameraOrbit");
		camera = PCamera.GetNode<Camera3D>("Camera3D");
		camera.Current = true;
		eyes = GetNode<Node3D>("Rig").GetNode<Skeleton3D>("Skeleton3D").GetNode<Node3D>("Eyes");
		collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
		capsuleShape = collisionShape.Shape as CapsuleShape3D;
		despawnTimer = GetNode<Timer>("DespawnTimer");
		rayCast = GetNode<Node3D>("%pistol").GetNode<RayCast3D>("RayCast3D");
		debugNode = GetParent<Node3D>().GetNode<Node3D>("%WorldDebugLines");
		navMap = GetParent<Node3D>().GetNode<NavigationRegion3D>("%NavigationRegion3D");
		audioStreamPlayer = GetNode<AudioStreamPlayer3D>("AudioStreamPlayer3D");
		soundTimer = GetNode<Timer>("SoundTimer");
		soundTimer.Timeout += () => RemoveSound();
		if (currentTeam == CurrentTeam.Blue)
		{
			soundNode = debugNode.GetNode<Node3D>("BlueSounds");
			enemyTeam = CurrentTeam.Red;
		}
		else
		{
			soundNode = debugNode.GetNode<Node3D>("RedSounds");
			enemyTeam = CurrentTeam.Blue;
		}

		AddVisibleRaycast();
		ResetCollision();
		if (camera == null)
			GD.Print("Camera is not initialized after Ready.");
	}

	public void AddVisibleRaycast()
	{
		debugLineMesh = new MeshInstance3D();
		rayMesh = new ImmediateMesh();

		debugLineMesh.Mesh = rayMesh;
		if (enemyTeam == CurrentTeam.Red)
		{
			debugLineMesh.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(1, 0, 0),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
		}
		else
		{
			debugLineMesh.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new Color(0, 1, 0),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
		}

		debugNode.AddChild(debugLineMesh);
		debugLineMesh.GlobalTransform = Transform3D.Identity;

		hitMarkerMesh = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(0.1f, 0.1f, 0.1f) },
			Visible = false
		};
		if (currentTeam == CurrentTeam.Red)
		{
			StandardMaterial3D markerMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(0, 1, 0),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			};
			hitMarkerMesh.MaterialOverride = markerMat;
		}
		else
		{
			StandardMaterial3D markerMat = new StandardMaterial3D
			{
				AlbedoColor = new Color(1, 0, 0),
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			};
			hitMarkerMesh.MaterialOverride = markerMat;
		}
		debugNode.AddChild(hitMarkerMesh);

		if (currentTeam == CurrentTeam.Red)
		{
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
		}
		else
		{
			soundsPosMesh = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 0.1f, Height = 0.1f },
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = new Color(0, 1, 0),
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
				},
				Visible = false
			};
		}
	}

	public void ResetCollision()
	{
		Speed = 5f;
		capsuleShape.Height = 1.864f;
		capsuleShape.Radius = 0.248f;
		collisionShape.Position = new Vector3(0, 0.875f, 0);
		cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
		eyes.Position = new Vector3(-0.039f, 1.636f, 0.149f);
		isAiming = false;
		PCamera.GetNode<Camera3D>("Camera3D").Fov = 70f;
		camera = PCamera.GetNode<Camera3D>("Camera3D");
		camera.Fov = 70f;
	}

	public override void _PhysicsProcess(double delta)
	{

		if (isDead || isDespawning)
		{
			if (despawnTimer.IsStopped()) Respawn();
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
		UpdateAimState();
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
			ResetCollision();
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

	private void UpdateAimState()
	{
		if (Input.IsMouseButtonPressed(MouseButton.Right))
		{
			if (!isAiming)
			{
				isAiming = true;
				cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0.75f);
				Speed = 2f;
				camera.Fov = 45f;
			}
		}
		else if (isAiming)
		{
			isAiming = false;
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
			Speed = 5f;
			camera.Fov = 70f;
		}
	}

	public void HandleMouseButtons(InputEventMouseButton mouseButtonEvent)
	{
		if (mouseButtonEvent.IsPressed() && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			SpawnSound();
			animationTree.Set("parameters/Shoot/request", (int)AnimationNodeOneShot.OneShotRequest.Fire);

			// Get the camera and direction from its forward (center of screen)

			Vector3 start = camera.GlobalPosition;
			Vector3 direction = -camera.GlobalTransform.Basis.Z;// Forward direction

			// Cast the ray
			float rayLength = 200f;
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
				hitMarkerMesh.Visible = false;


		}

		if (mouseButtonEvent.IsPressed() && mouseButtonEvent.ButtonIndex == MouseButton.Right)
		{
			isAiming = true;
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0.75f);
			Speed = 2f;
			camera.Fov = 45f;
		}
		else if (mouseButtonEvent.IsReleased() && mouseButtonEvent.ButtonIndex == MouseButton.Right)
		{
			isAiming = false;
			cameraOrbit.Position = new Vector3(-0.5f, 1.75f, 0);
			Speed = 5f;
			camera.Fov = 70f;
		}
	}

	public void SpawnSound()
	{
		audioStreamPlayer.Play();
		if (soundsPosMesh != null)
		{
			soundsPosMesh.Visible = false;
		}
		if (soundsPosMesh.GetParent() != soundNode)
		{
			soundNode.AddChild(soundsPosMesh);
		}
		soundsPosMesh.GlobalTransform = new Transform3D(Basis.Identity, rayCast.GlobalPosition);
		soundsPosMesh.Visible = false;
		soundTimer.Start();
	}

	public void RemoveSound()
	{
		if (soundsPosMesh != null && soundsPosMesh.GetParent() == soundNode)
		{
			soundsPosMesh.Visible = false;
			soundNode.RemoveChild(soundsPosMesh);
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

		if (currentHealth > 100)
			currentHealth = MaxHealth;
		//GD.Print("Current Health: " + Name + currentHealth);
		if (currentHealth <= 0 && !isDead)
		{
			isDead = true;
			isDespawning = true;
			currentHealth = 0;
			animationTree.Set("parameters/AimFall/blend_amount", 0);
			currentAnim = CurrentAnim.DieA;
			capsuleShape.Height = 0.01f;
			capsuleShape.Radius = 0.01f;
			rayMesh.ClearSurfaces();
			despawnTimer.Start();
		}
	}

	public void ExitTree()
	{
		//NavigationServer3D.MapChanged -= OnMapChanged;
		//shootTimer.Timeout -= Shoot;
		soundTimer.Timeout -= RemoveSound;
		//clearOpponentPosTimer.Timeout -= ClearOpponentData;
	}

	public async void Respawn()
	{
		int targetIndex = (int)rng.RandfRange(0, 5);
		List<Node3D> teamSpawns = new List<Node3D>();

		if (currentTeam == CurrentTeam.Blue)
		{
			teamSpawns.Clear();
			foreach (var spawn in GetParent<Node3D>().GetNode<Node3D>("%BlueSpawns").GetChildren())
			{
				teamSpawns.Add(spawn as Node3D);
			}
		}
		else
		{
			teamSpawns.Clear();
			foreach (var spawn in GetParent<Node3D>().GetNode<Node3D>("%RedSpawns").GetChildren())
			{
				teamSpawns.Add(spawn as Node3D);
			}
		}

		if (targetIndex >= teamSpawns.Count)
			targetIndex = 0; // fallback

		var spawnPoint = teamSpawns[targetIndex];
		var spawnPoint3D = spawnPoint as Node3D;
		var spawningPlayer = playerScene.Instantiate<Player>();

		GetParent<Node3D>().AddChild(spawningPlayer);
		spawningPlayer.GlobalPosition = spawnPoint3D.GlobalPosition + (Vector3.Up - new Vector3(0, 0.75f, 0));
		spawningPlayer.isDead = false;
		spawningPlayer.camera.Current = true;
		spawningPlayer.AddToGroup(currentTeam.ToString());

		//GD.Print(currentTeam.ToString());
		// After spawning player in player Respawn()
		await ToSignal(GetTree(), "process_frame"); // optional safety

		// foreach (Enemy enemy in GetTree().GetNodesInGroup(enemyTeam.ToString()))
		// {
		// 	if (enemy != null && GodotObject.IsInstanceValid(enemy))
		// 		enemy.SetRandomNavPos();
		// }

		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetViewport().SetInputAsHandled();
		spawningPlayer.ReacquireInput();
		//ExitTree();
		QueueFree();
	}

	public void ReacquireInput()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		GetViewport().SetInputAsHandled();
	}
	
}
