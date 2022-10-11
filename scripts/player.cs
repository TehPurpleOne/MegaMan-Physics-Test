using Godot;
using System;
using Array = Godot.Collections.Array;

public class player : KinematicBody2D
{
    //This is a simple test engine based on the NES style MegaMan games. It will not be 100% accurate, but as close as I can get withing my abilities.
    //Everything below will be commented to explain what they do and where.
    
    //Make values for the nodes in the player scene so they can be called as necessary.
    public Sprite sprite;               //This is the player's sprite.
    public AnimationPlayer anim;        //The animation player.
    public CollisionShape2D standBox;   //This hitbox is used when the player is no sliding.
    public CollisionShape2D slideBox;   //This box is used when sliding.

    //Preload the textures needed for the player. These will be used to swap between Rock's normal frames and his shooting poses without resetting animations.
    public Texture nTexture = (Texture)GD.Load("res://assets/player/mega-norm.png");
    public Texture sTexture = (Texture)GD.Load("res://assets/player/mega-shoot.png");

    //Constants
    const float RUNSPD = 82.5F;         //The player's default running speed.
    const float JUMPSPD = -310;         //Player's jump speed when the jump button is pressed.
    const float GRAVITY = 900;          //Gravity strength.

    //Variables
    public Vector2 dirTap = Vector2.Zero;   //Determines a tapped direction.
    public Vector2 dirHold = Vector2.Zero;  //Determines if any directions are being held.
    public float lastXDir = 0;              //Records the last horizontal direction used by the player.
    public bool jumpTap = false;            //Player tapped the jump button.
    public bool jumpHold = false;           //Player is holding the jump button.
    public bool fireTap = false;            //Player tapped the fire button.
    public Vector2 velocity = Vector2.Zero; //Speed and angle the player will be moving.
    public float xSpeedMod = 1;             //This value can be used to speed up or slow down the player as needed (Like when they are walking on ice or sliding).
    public float xSpeed = 0;                //The final horizontal speed value for the player once movement calculations are complete.
    public float gravMod = 1;               //Gravity modifier. Used to make the player heavy or for water physics.
    public int slideDelay = 0;              //Used to limit how far a slide can travel and when the player can slide again.
    public int maxAirJumps = 1;             //Maximum air jumps the player can perform. Used for double jumping.
    public int airJumps = 1;                //Jumps that can be performed in air.
    public int shotDelay = 0;               //Timer for how long the shooting frames are on screen before reverting to normal.
    public int hurtTime = 0;                //How long the player plays the "damage" animation when hit.
    public int iFrames = 0;                 //How long the player blinks before being able to take damage again.

    //Flags
    public bool spriteFlip = false;     //This along with actualFlip determine which direction the player is facing. Why two? See climbing functions.
    public bool actualFlip = false;     //''
    public bool forceIdle = false;      //Used to prevent the player from going into their jumping animation right away. Useful for after the player teleports in or reaches the top of a ladder.
    public bool stopX = false;          //Stop X movement as needed (During the little step state for example).
    public bool land = false;           //Play the landing sound effect. May not be needed with the new FSM design.
    public bool stop = true;            //Stops the player from moving if true. USed to prevent movement during certain animations

    //Player States
    //MegaMan will use a Finite State Machine which will limit which states he can enter, exit, and when those occur.
    public enum state {BEAM, APPEAR, LEAVE, IDLE, LILSTEP, RUN, JUMP, SLIDE, CLIMB, CLIMBTOP, HURT};
    public enum texture {NORMAL, SHOOT} //There are other texture states that make up a classic MegaMan game, but we'll keep it simple for now. I'll update the project files later if enough people want to know how throwing, etc is handled.
    public Array last = new Array {null, null};

    public override void _Ready()
    {
        //Assign all the nodes within the scene to their respective variables.
        sprite = (Sprite)GetChild(0);
        anim = (AnimationPlayer)GetChild(1);
        standBox = (CollisionShape2D)GetChild(2);
        slideBox = (CollisionShape2D)GetChild(3);

        //Initialize the player's animations and texture.
        changeAnim(state.BEAM);
        changeTexture(texture.NORMAL);
    }

    public override void _PhysicsProcess(float delta)
    {
        //The meat of the script. This will dictate how everything comes together to make a functioning player scene.

        //Get the player's input state. I highly suggest making this section a global singleton to save a bit of headache during development.
        dirTap.x = Convert.ToInt32(Input.IsActionJustPressed("right")) - Convert.ToInt32(Input.IsActionJustPressed("left"));
        dirTap.y = Convert.ToInt32(Input.IsActionJustPressed("down")) - Convert.ToInt32(Input.IsActionJustPressed("up"));

        dirHold.x = Convert.ToInt32(Input.IsActionPressed("right")) - Convert.ToInt32(Input.IsActionPressed("left"));
        dirHold.y = Convert.ToInt32(Input.IsActionPressed("down")) - Convert.ToInt32(Input.IsActionPressed("up"));

        jumpTap = Input.IsActionJustPressed("jump");
        jumpHold = Input.IsActionPressed("jump");
        fireTap = Input.IsActionJustPressed("fire");

        //Change the player direction.
        if((int)last[0] == (int)state.IDLE || (int)last[0] == (int)state.LILSTEP || (int)last[0] == (int)state.RUN || (int)last[0] == (int)state.JUMP || (int)last[0] == (int)state.SLIDE)
        { // When not climbing, hurt, or teleporting, the sprite direction is applied immediately.
            switch(dirHold.x)
            {

                case -1:
                actualFlip = true;
                spriteFlip = true;
                break;

                case 1:
                actualFlip = false;
                spriteFlip = false;
                break;

            }
        }
        //Code to actually flip the player's sprite is below to prevent an animation triggering one frame before the direction change occurs.

        switch(last[0])
        {
            //Player is teleporting in at the beginning of the stage.
            case (int)state.BEAM:
            //The player's sprite offset is set to 1 screen higher than normal. Pull the sprite down so play can begin.
            if(sprite.Offset.y < 0)
            {
                sprite.Offset += Vector2.Down * 8;
            }
            else
            {
                changeAnim(state.APPEAR); //The offset is no longer above the player's hitbox. Transition to the appear animation.
            }
            break;

            //A case isn't needed for the APPEAR state, as it does nothing but play the animation.

            //Idle state. The player is just standing there... menacingly.
            case (int)state.IDLE:
            jumpCheck();
            groundCheck();
            slideCheck();
            
            if(dirHold.x != 0)
            {
                changeAnim(state.LILSTEP);
            }
            break;

            //The "little step" the player makes before going into a full sprint.
            case (int)state.LILSTEP:
            if(!stopX) //Prevent the player moving more than one frame.
            {
                stopX = true;
            }

            jumpCheck();
            groundCheck();
            slideCheck();
            
            //State transition begins once the lilStep animation is completed. See below.
            break;

            //The Run state.
            case (int)state.RUN:
            jumpCheck();
            groundCheck();
            slideCheck();
            
            if(dirHold.x == 0) //Player released the left or right buttons.
            {
                changeAnim(state.LILSTEP);
            }
            break;

            //Jumping/Falling state.
            case (int)state.JUMP:
            jumpCheck();

            if(velocity.y < 0 && !jumpHold) //Player has released the jump button before the peak of the jump.
            {
                velocity.y = 0;
            }

            if(IsOnFloor()) //Player has landed on the floor.
            {
                if(dirHold.x == 0)
                {
                    changeAnim(state.IDLE);
                }
                else
                {
                    changeAnim(state.RUN);
                }
                airJumps = maxAirJumps;
            }
            break;
        }

        if((int)last[0] == (int)state.CLIMB || (int)last[0] == (int)state.CLIMBTOP)
        {
            //Changing the direction of the player while on a ladder works a lot differently, as their initial direction is locked in place the moment they grab the ladder.
            //Pressing left ore right will only change the actualFlip value, which will flip the sprite to the appropriate direction when a shot is fired.
            switch(dirHold.x)
            {

                case -1:
                actualFlip = true;
                break;

                case 1:
                actualFlip = false;
                break;

            }
        }

        //Flip the sprite accordingly.
        if((int)last[0] != (int)state.CLIMB && (int)last[0] != (int)state.CLIMBTOP)
        {
            //No climbing restriction, flip as needed.
            if(sprite.FlipH != spriteFlip)
            {
                sprite.FlipH = spriteFlip;
            }
        }
        else
        {
            //Player is climbing.
            if(shotDelay == 0)
            {
                if(sprite.FlipH != spriteFlip)
                {
                    sprite.FlipH = spriteFlip;
                }
            }
            else
            {
                if(sprite.FlipH != actualFlip)
                {
                    sprite.FlipH = actualFlip;
                }
            }
        }

        if((int)last[0] != (int)state.BEAM && (int)last[0] != (int)state.APPEAR && (int)last[0] != (int)state.LEAVE && (int)last[0] != (int)state.CLIMB && (int)last[0] != (int)state.CLIMBTOP && (int)last[0] != (int)state.SLIDE && (int)last[0] != (int)state.HURT)
        { //Move the player left or right.
            applyXSpd();
        }

        if((int)last[0] == (int)state.SLIDE)
        { //Player is sliding. Move the player.
            applySlideSpd();
        }

        if((int)last[0] != (int)state.BEAM && (int)last[0] != (int)state.APPEAR && (int)last[0] != (int)state.LEAVE && (int)last[0] != (int)state.CLIMB && (int)last[0] != (int)state.CLIMBTOP)
        { //If the player's state is NOT one of the above listed, apply gravity.
            applyGravity(delta);
        }

        velocity.x = xSpeed;

        velocity = MoveAndSlide(velocity, Vector2.Up);

        GlobalPosition = new Vector2(Mathf.Clamp(GlobalPosition.x, 0, 512), GlobalPosition.y);
    }

    public void applyGravity(float delta)
    {
        //Applies gravity to the player. Not needed in all states.
        velocity.y += GRAVITY * delta; //delta is only needed when calculating gravity. Using it when calculating horizontal speed would measn increasing said speed to compensate.
    }

    public void applyXSpd()
    {
        //Applies horizontal speed based on the direction being held.
        if(!stopX)
        {
            xSpeed = (dirHold.x * RUNSPD) / xSpeedMod;
        }
        else{
            xSpeed = 0;
        }
    }

    public void applySlideSpd()
    {
        //Applies horizontal speed based on the direction the sprite is facing.
    }

    public void hitboxCheck()
    {
        //Add an Area2D or two to the scene and replace the function below so the player can recevie damage or pick up items.
        //Since we aren't using an Area2D for collision detection with other objects, we are going to simulate the player getting hit by pressing the spacebar.
    }

    public void damage()
    {
        //Damage functions.
    }

    public void jumpCheck()
    {
        //Player pressed jump while on the ground.
        if(jumpTap && IsOnFloor())
        {
            velocity.y = JUMPSPD;
            changeAnim(state.JUMP);
        }
        
        //Player pressed jump while in the air (Double jump mechanic).
        if(jumpTap && !IsOnFloor() && airJumps > 0)
        {
            velocity.y = JUMPSPD;
            changeAnim(state.JUMP);
            airJumps --;
        }
    }

    public void groundCheck()
    {
        if(!IsOnFloor())
        {
            changeAnim(state.JUMP);
        }
    }

    public void slideCheck()
    {

    }

    public void changeTexture(texture which)
    {
        //Changes the texture based on the shotDelay value.
        switch(which)
        {
            case texture.NORMAL:
            sprite.Texture = nTexture;
            break;

            case texture.SHOOT:
            sprite.Texture = sTexture;
            break;
        }

        last[1] = which; //Save the texture value for later.
    }

    public void changeAnim(state which)
    {
        //Changes the animation based on the player's current state.
        if(hurtTime <= 96)
        {
            
            switch(which)
            {
                case state.BEAM:
                anim.Play("RESET");
                break;

                case state.APPEAR:
                //Play beam in sound byte here.
                anim.Play("beamIn");
                break;

                case state.LEAVE:
                //Play beam out sound byte here.
                anim.Play("beamOut");
                break;

                case state.IDLE:
                anim.Play("idle");
                break;

                case state.LILSTEP:
                anim.Play("lilStep");
                break;

                case state.RUN:
                anim.Play("run");
                break;

                case state.JUMP:
                anim.Play("jump");
                stopX = false; //Failsafe in case stopX isn't set to false when jumping.
                break;

                case state.SLIDE:
                anim.Play("slide");
                break;

                case state.CLIMB:
                anim.Play("climb");
                break;

                case state.CLIMBTOP:
                anim.Play("climbTop");
                break;

                case state.HURT:
                anim.Play("hurt");
                break;

            }
        }
        last[0] = which; //Save the state value for later.
    }

    public void onAnimDone(string which)
    {
        //Checks to see if an animation has been completed, then executes a function as needed.
        switch(which)
        {

            case "beamIn":
            changeAnim(state.IDLE);
            stop = false; //We allow the player to move once the beam in animation is complete.
            break;

            case "lilStep":
            if(dirHold.x != 0) //Determine if the player is still holding left or right. If so, start running. If not, return to the Idle state.
            {
                changeAnim(state.RUN);
            }
            else
            {
                changeAnim(state.IDLE);
            }
            stopX = false; //Be sure to turn off stopX here, otherwise you won't be able to move!
            break;

        }
    }
}
