using Godot;
using System;

public partial class OrbitalCamera3D : CustomCamera
{
    

    public Vector3 Direction
    {
        get => _direction;
        set
        {
            if (_direction != value)
            {
                _direction = value;
            }
        }
    }
    private Vector3 _direction;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (Current)
        {
            // _direction.X += Input.GetActionStrength("move_left") - Input.GetActionStrength("move_right");
            // _direction.Y = Input.GetActionStrength("move_up") - Input.GetActionStrength("move_down");
            // _direction.Z += Input.GetActionStrength("move_forward") - Input.GetActionStrength("move_backward");

            // DistanceFromTarget += _direction.Y * (float)delta * 2f;
            // GlobalPosition = GlobalPosition with { Z = DistanceFromTarget };




        }
    }
}