using Godot;
using System;

public partial class PlayerControllerBody : CharacterBody3D
{

    public PlayerController Gimbal { get; set; }

    public override void _Ready()
    {
        Gimbal = GetNode<PlayerController>("../../../");
    }


    
}
