using Godot;

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
}