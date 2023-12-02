using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{

	[Export]
	public float viewingDistance = 10;
	[Export]
	public float movementSpeed = 100;
	[Export]
	public float turnSpeed = 35;
	// [Export]
	// public CameraComponent cameraComponent;
	[Export]
	public VelocityComponent velocityComponent;
	[Export]
	public CollisionShape3D collisionShape3D;

	private Vector3 Direction = Vector3.Forward;
	private bool isLocked = false;

	// public override void _Ready()
	// {
	// 	cameraComponent.LockMouse();
	// }

	public void LockPlayerMovement()
	{
		isLocked = true;
	}

	public void UnlockPlayerMovement()
	{
		isLocked = false;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!isLocked)
		{	
			Vector3 movement = Vector3.Zero;
			Vector3 rotation = Vector3.Zero;
			movement.X = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
			movement.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
			movement.Z = Input.GetActionStrength("move_backward") - Input.GetActionStrength("move_forward");
			rotation.Y = Input.GetActionStrength("rotate_right") - Input.GetActionStrength("rotate_left");
			// cameraComponent.UpdateGimbal(movement, rotation);

			// Position = cameraComponent.camera.GlobalPosition;
			// MoveAndSlide();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion)
		{
			// float HorizontalRotation = -mouseMotion.Relative.X * cameraComponent.mouseSensitivity;
			// float VerticalRotation = mouseMotion.Relative.Y * cameraComponent.mouseSensitivity;

			// cameraComponent.horizontalRotation += HorizontalRotation;
			// cameraComponent.verticalRotation += VerticalRotation;
		}

		if (Input.IsActionJustReleased("speed_up_cam"))
		{
			movementSpeed += 10;
		}

		if (Input.IsActionJustReleased("speed_down_cam"))
		{
			movementSpeed -= 10;
		}





	}

}
