using Godot;
using System;

[Tool]
public partial class MyNode2D : Node2D
{
    private bool _holdingHammer;

    [Export]
    public bool HoldingHammer
    {
        get => _holdingHammer;
        set
        {
            _holdingHammer = value;
            NotifyPropertyListChanged();
        }
    }

    public int HammerType { get; set; }

    public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
    {
        // By default, `HammerType` is not visible in the editor.
        var propertyUsage = PropertyUsageFlags.NoEditor;

        


        if (HoldingHammer && GetParent() == GetViewport())
        {
            propertyUsage = PropertyUsageFlags.Default;
        }

        var properties = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        properties.Add(new Godot.Collections.Dictionary()
        {
            { "name", "HammerType" },
            { "type", (int)Variant.Type.Int },
            { "usage", (int)propertyUsage }, // See above assignment.
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "Wooden,Iron,Golden,Enchanted" }
        });

        return properties;
    }
}