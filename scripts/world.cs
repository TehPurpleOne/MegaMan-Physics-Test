using Godot;
using System;

public class world : Node2D
{
    //This is the game world that MegaMan runs around in. This will contain all of the rooms, collision masks, and code needed to complete the physics test.
    //First, grab the needed nodes from the scene.
    public TileMap collMask;        //This is the collision mask for the game world. Needed to determine which tiles MegaMan is currently overlapping.
    public player plyr;             //The player scene and it's functions.

    //Needed variables
    public int overlap = 0;         //Tile ID that MegaMan is overlapping.
    public int lastOverlap = 0;     //Last tile ID that the player was overlapping.

    public override void _Ready()
    {
        plyr = (player)GetChild(2);
        collMask = (TileMap)GetChild(1);
    }
}
