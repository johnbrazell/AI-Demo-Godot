using Godot;
using System;

public partial class Health : Node3D
{

	private float origionalX;
	private float origionalY;
	private float origionalZ;
	private float updatedY;
	private float bobSpeed = 1f;
	private float bobDistance = 0.25f;
	private float rotationSpeed = 0.5f;
	private float timeElapsed = 0f;
	private Area3D area;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		origionalX = Position.X;
		origionalY = Position.Y;
		origionalZ = Position.Z;
		updatedY = origionalY;
		area = GetNode<Area3D>("Area3D");
		area.BodyEntered += OnBodyEntered;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		timeElapsed += (float)delta;

		// Sin Y offset
		float bobOffset = Mathf.Sin(timeElapsed * bobSpeed) * bobDistance;

		// Apply bobbing and rotation
		Position = new Vector3(origionalX, origionalY + bobOffset, origionalZ);
		RotationDegrees += new Vector3(0f, rotationSpeed, 0f);
	}

	public void OnBodyEntered(Node body)
	{
		if (body is CharacterBody3D character)
		{
			character.Call("TakeDamage", -75);

			Visible = false;
			area.SetPhysicsProcess(false);
			area.SetProcess(false);
			area.CollisionMask = 8;

			Timer respawnTimer = new Timer();
			respawnTimer.WaitTime = 20f;
			respawnTimer.OneShot = true;
			respawnTimer.Timeout += Respawn;
			AddChild(respawnTimer);
			respawnTimer.Start();
		}
	}

	public void Respawn()
	{
		Visible = true;
		area.SetPhysicsProcess(true);
		area.SetProcess(true);
		area.CollisionMask = 1;
	}

}
