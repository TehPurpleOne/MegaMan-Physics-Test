using Godot;
using System;

public class world : Node2D
{
    //This is the game world that MegaMan runs around in. This will contain all of the rooms, collision masks, and code needed to complete the physics test.
    //First, grab the needed nodes from the scene.
    public TileMap collMask;        //This is the collision mask for the game world. Needed to determine which tiles MegaMan is currently overlapping.
    public player plyr;             //The player scene and it's functions.

    //Needed variables
    

    public override void _Ready()
    {
        //Assign the values to the needed nodes.
        plyr = (player)GetChild(2);
        collMask = (TileMap)GetChild(1);
    }

    public override void _Process(float delta)
    {
        mapCheck();
    }

    public void mapCheck()
    {
        //This function sets the value of the various tile IDs that the player will encounter within the game.
        plyr.overlap = collMask.GetCellv(collMask.WorldToMap(plyr.GlobalPosition));
        plyr.below = collMask.GetCellv(collMask.WorldToMap(plyr.GlobalPosition) + Vector2.Down);

        //Set the xSnap value so the player is aligned with any ladders they grab onto. Also save the tile's position.
        plyr.overlapPos = collMask.MapToWorld(collMask.WorldToMap(plyr.GlobalPosition));
        plyr.xSnap = plyr.overlapPos.x + 8;
    }
}
