using Godot;
using System;

public partial class PlayerControllerBody : CharacterBody3D
{
	
	public PlayerController Gimbal { get => _gimbal; private set{} }
	private PlayerController _gimbal;

    public override void _Ready()
    {
        _gimbal = GetNode<PlayerController>("../../../");
    }
}
