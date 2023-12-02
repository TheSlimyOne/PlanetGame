using Godot;
using System;

[Tool]
public partial class PhysicsController : RigidBody3D
{
	[Export]
	private CollisionShape3D _collisionShape3D;
}
