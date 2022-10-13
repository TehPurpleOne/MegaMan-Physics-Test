using Godot;
using System;
using Array = Godot.Collections.Array;

public class player : KinematicBody2D
{
    //This is a simple test engine based on the NES style MegaMan games. It will not be 100% accurate, but as close as I can get within my abilities.
    //Everything below will be commented to explain what they do and where.
    
    //Make values for the nodes in the player scene so they can be called as necessary.
    public Sprite sprite;               //This is the player's sprite.
    public AnimationPlayer anim;        //The animation player.
    public CollisionShape2D standBox;   //This hitbox is used when the player is no sliding.
    public CollisionShape2D slideBox;   //This box is used when sliding.
    public Area2D obsCheck;             //Check for low ceilings while sliding.
    public CollisionShape2D wallBox;    //Check if the player is against a wall before a slide.

    //Preload the textures needed for the player. These will be used to swap between Rock's normal frames and his shooting poses without resetting animations.
    public Texture nTexture = (Texture)GD.Load("res://assets/player/mega-norm.png");
    public Texture sTexture = (Texture)GD.Load("res://assets/player/mega-shoot.png");

    //Constants
    const float RUNSPD = 82.5F;         //The player's default running speed.
    const float JUMPSPD = -310;         //Player's jump speed when the jump button is pressed.
    const float GRAVITY = 900;          //Gravity strength.

    //Controls
    public Vector2 dirTap = Vector2.Zero;   //Determines a tapped direction.
    public Vector2 dirHold = Vector2.Zero;  //Determines if any directions are being held.
    public float lastXDir = 0;              //Records the last horizontal direction used by the player.
    public bool jumpTap = false;            //Player tapped the jump button.
    public bool jumpHold = false;           //Player is holding the jump button.
    public bool fireTap = false;            //Player tapped the fire button.
    public bool hurtTap = false;            //Damage the player.

    //Variables
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
    public int overlap = 0;                 //Tile ID of what the player is currently overlapping
    public int lastOverlap = 0;             //Tile ID of the previously overlapped tile.
    public int below = 0;                   //Tile ID of the tile directly below the player.
    public Vector2 overlapPos;              //Overlapping tile position.
    public float xSnap = 0;                 //X Position to snap to when ladders are activated.

    //Flags
    public bool spriteFlip = false;     //This along with actualFlip determine which direction the player is facing. Why two? See climbing functions.
    public bool actualFlip = false;     //''
    public bool forceIdle = false;      //Forces the FSM into the Idle state. Useful for reaching the tops of ladders.
    public bool stopX = false;          //Stop X movement as needed (During the little step state for example).
    public bool safeStop = true;       //Flag to determine if it's safe for the player to come out of their slide.
    public bool land = false;           //Play the landing sound effect. May not be needed with the new FSM design.
    public bool stop = true;            //Stops the player from moving if true. USed to prevent movement during certain animations

    //Player States
    //MegaMan will use a Finite State Machine which will limit which states he can enter, exit, and when those occur.
    public enum state {BEAM, APPEAR, LEAVE, IDLE, LILSTEP, RUN, JUMP, SLIDE, CLIMB, CLIMBTOP, HURT};
    public enum texture {NORMAL, SHOOT} //There are other texture states that make up a classic MegaMan game, but we'll keep it simple for now. I'll update the project files later if enough people want to know how throwing, etc is handled.
    public Array last = new Array {null, null, null};

    public override void _Ready()
    {
        //Assign all the nodes within the scene to their respective variables.
        sprite = (Sprite)GetChild(0);
        anim = (AnimationPlayer)GetChild(1);
        standBox = (CollisionShape2D)GetChild(2);
        slideBox = (CollisionShape2D)GetChild(3);
        obsCheck = (Area2D)GetChild(4);
        wallBox = (CollisionShape2D)obsCheck.GetChild(1);

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
        hurtTap = Input.IsActionJustPressed("damage");

        //Change the player direction.
        if((int)last[0] == (int)state.IDLE || (int)last[0] == (int)state.LILSTEP || (int)last[0] == (int)state.RUN || (int)last[0] == (int)state.JUMP || (int)last[0] == (int)state.SLIDE)
        { // When not climbing, hurt, or teleporting, the sprite direction is applied immediately.
            switch(dirHold.x)
            {

                case -1:
                actualFlip = true;
                spriteFlip = true;
                slideBox.Position = new Vector2(1.5F, 3); //We'll also orient the collision boxes here for smoother gameplay.
                wallBox.Position = new Vector2(-7.5F, 3);
                break;

                case 1:
                actualFlip = false;
                spriteFlip = false;
                slideBox.Position = new Vector2(-1.5F, 3);
                wallBox.Position = new Vector2(7.5F, 3);
                break;

            }
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

        //Sprite flipping for climbing was moved below the weapon check to prevent issues with animation.

        //Set the gravity modifier for whenever the player is underwater or not.
        if(lastOverlap != overlap)
        {
            if(overlap == 3) //Tile ID of 3 indicates water for this example. Your tiles may vary.
            {
                gravMod = 3;
            }
            else
            {
                gravMod = 1;
            }

            lastOverlap = overlap;
        }

        //Subtract til Slide Delay reaches 0 if not sliding. This will prevent the player from chaining slides together endlessly.
        if((int)last[0] != (int)state.SLIDE && slideDelay > 0)
        {
            slideDelay --;
        }

        hitboxCheck();

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

            //Climbing States
            case (int)state.CLIMB:
            if(dirHold.y == 0 && anim.IsPlaying())
            {
                anim.Stop(false); //Stop the climbing animation if the player isn't moving, but save the timing position.
            }

            if(dirHold.y != 0 && !anim.IsPlaying())
            {
                anim.Play((string)anim.CurrentAnimation); //Resume animation if the player is moving.
            }

            if(shotDelay > 0) //Stop movement if shot delay is greater thasn 0;
            {
                stopX = true;
            }
            else
            {
                stopX = false;
            }

            if(overlap != 2 && overlap != 1)
            {
                //Player has reached the bottom of a ladder, make them fall.
                velocity.y = 0;
                changeAnim(state.JUMP);
            }

            if(IsOnFloor() && dirHold.y == 1)
            {
                //Player touched the floor while climbing. Swap to Idle.
                changeAnim(state.IDLE);
            }

            if(jumpTap)
            {
                //Player jumped while on the ladder.
                changeAnim(state.JUMP);
            }

            if(overlap == 1 && GlobalPosition.y < overlapPos.y + 6)
            {
                //When the player is close to the top, but not above it, swap to the climb up/down animation.
                changeAnim(state.CLIMBTOP);
            }

            break;

            case (int)state.CLIMBTOP:
            if(shotDelay > 0) //Stop movement if shot delay is greater thasn 0;
            {
                stopX = true;
            }
            else
            {
                stopX = false;
            }

            if(jumpTap)
            {
                //Player jumped while on the ladder.
                changeAnim(state.JUMP);
            }

            if(overlap == 1 && GlobalPosition.y > overlapPos.y + 6)
            {
                //When the player is low enough on the ladder, swap to the climbing animation
                changeAnim(state.CLIMB);
            }

            if(overlap != 1 && overlap != 2 && dirHold.y == -1)
            {
                //When the player is on top of the ladder, disable climbing functions.
                GlobalPosition = new Vector2(GlobalPosition.x, overlapPos.y + 6);
                velocity.y = 0;
                forceIdle = true;
                changeAnim(state.IDLE);
            }

            break;

            //Sliding
            case (int)state.SLIDE:
            groundCheck();
            jumpCheck();
            
            //Use the Low Ceiling area 2D to determine if a low ceiling is above the player.
            var headCheck = obsCheck.GetOverlappingBodies();
            if(headCheck.Count > 0)
            {
                safeStop = false;
            }
            else
            {
                safeStop = true;
            }

            if(slideDelay > 4)
            {
                //Subtract from Slide Delay so the slide doesn't continue forever.
                slideDelay --;
            }

            if(dirHold.x != 0 && dirHold.x != lastXDir && safeStop)
            {
                //The player tapped a direction opposite of which they are sliding.
                slideDelay = 4;
            }

            if(IsOnWall() && safeStop)
            {  
                //If the player hits a wall and safeStop is true, return to the Idle state.
                changeAnim(state.IDLE);
            }

            if(slideDelay <= 4 && safeStop)
            {
                //Setting the slide delay to 4 will cancel the slide, but then give a few frames before the player can slide again.
                changeAnim(state.IDLE);
            }

            break;

            //Player has taken damage.
            case (int)state.HURT:
            if((int)last[1] != 1) //Apply a special xSpeed value unless the player is sliding under a low ceiling.
            {
                if(sprite.FlipH) 
                {
                    xSpeed = 50;
                }
                else
                {
                    xSpeed = -50;
                }
            }
            else
            {
                xSpeed = 0;
            }

            if(hurtTime > 0) //Subtract from Hurt Time
            {
                hurtTime --;
            }

            if(hurtTime == 72) //Allow the player to regain control once the timer hits a certain value.
            {
                //This is where last[1] comes into play. If the player is under a low ceiling, continue the slide.
                if((int)last[1] == 1)
                {
                    changeAnim(state.SLIDE);
                }
                else
                {
                    changeAnim(state.IDLE);
                }
            }
            break;
        }

        //Make the player blink until Hurt Time hits 0.
        if(hurtTime > 0 && (int)last[0] != (int)state.HURT)
        {
            iFrames ++;
            iFrames = Mathf.Wrap(iFrames, 0, 2);

            if(iFrames == 0)
            {
                int showHide = Convert.ToInt32(sprite.Visible);
                showHide ++;
                showHide = Mathf.Wrap(showHide, 0, 2);
                sprite.Visible = Convert.ToBoolean(showHide);
            }

            hurtTime --;
        }

        if(hurtTime == 0 && !sprite.Visible) //Hurt timer has expired. Stop the flashing.
        {
            sprite.Show();
        }

        //Check to see if the player fired their weapon this frame. If so, swap textures.
        //NOTE: This function will need to be altered if you plan on making a game with multiple weapons. Below is just for this example.
        weaponCheck();

        if(shotDelay > 0 && (int)last[2] != (int)texture.SHOOT)
        {
            changeTexture(texture.SHOOT);
        }

        if(shotDelay > 0) //Subtract from the shot delay timer.
        {
            shotDelay --;
        }

        if(shotDelay == 0 && (int)last[2] != (int)texture.NORMAL) //TImer is at 0, reset to the normal texture.
        {
            changeTexture(texture.NORMAL);
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

        if((int)last[0] == (int)state.CLIMB || (int)last[0] == (int)state.CLIMBTOP)
        {
            applyClimbSpd();
        }

        if((int)last[0] == (int)state.SLIDE)
        { //Player is sliding. Move the player.
            applySlideSpd();
        }

        if((int)last[0] != (int)state.BEAM && (int)last[0] != (int)state.APPEAR && (int)last[0] != (int)state.LEAVE && (int)last[0] != (int)state.CLIMB && (int)last[0] != (int)state.CLIMBTOP)
        { //If the player's state is NOT one of the above listed, apply gravity.
            applyGravity(delta);
        }

        //Check to see if the player is trying to activate ladder functions.
        //Ladder functions begin here to prevent any unwanted movement after activation.
        if((int)last[0] == (int)state.IDLE || (int)last[0] == (int)state.LILSTEP || (int)last[0] == (int)state.RUN || (int)last[0] == (int)state.JUMP || (int)last[0] == (int)state.SLIDE)
        {
            ladderCheck();
        }

        //Set the appropriate collision box when sliding or not. Ignore if in the hurt state.
        if((int)last[0] == (int)state.SLIDE && (int)last[0] != (int)state.HURT && !standBox.Disabled)
        {
            slideBox.Disabled = false;
            standBox.Disabled = true;
            wallBox.Disabled = true;
        }
        if((int)last[0] != (int)state.SLIDE && (int)last[0] != (int)state.HURT && standBox.Disabled)
        {
            slideBox.Disabled = true;
            standBox.Disabled = false;
            wallBox.Disabled = false;
        }

        //Set X velocity to the XSpeed value and move the player.
        velocity.x = xSpeed;
        velocity = MoveAndSlide(velocity, Vector2.Up);

        //Clamp the player's X position to keep them on screen.
        GlobalPosition = new Vector2(Mathf.Clamp(GlobalPosition.x, 0, 512), GlobalPosition.y);
    }

    public void applyGravity(float delta)
    {
        //Applies gravity to the player. Not needed in all states.
        velocity.y += (GRAVITY * delta) / gravMod; //delta is only needed when calculating gravity. Using it when calculating horizontal speed would measn increasing said speed to compensate.
    }

    public void applyXSpd()
    {
        //Applies horizontal speed based on the direction being held, unless the stopXflag is on.
        if(!stopX)
        {
            xSpeed = (dirHold.x * RUNSPD) / xSpeedMod;
        }
        else
        {
            xSpeed = 0;
        }
    }

    public void applySlideSpd()
    {
        //Applies horizontal speed based on the direction the sprite is facing.
        int sdir = 0;
        if(sprite.FlipH){
            sdir = -1;
        }
        else
        {
            sdir = 1;
        }

        xSpeed = ((sdir * RUNSPD) * 2F) / xSpeedMod;
    }

    public void applyClimbSpd()
    {
        //Applies climbing velocity to the player. stopX is repurposed here to prevent climing in certain situations.
        if(!stopX)
        {
            velocity.y = (dirHold.y * RUNSPD) * 0.75F;
        }
        else
        {
            velocity.y = 0;
        }
    }

    public void hitboxCheck()
    {
        //Add an Area2D or two to the scene and replace the function below so the player can recevie damage or pick up items.
        //Since we aren't using an Area2D for collision detection with other objects, we are going to simulate the player getting hit by pressing the spacebar.
        if((int)last[0] != (int)state.BEAM && (int)last[0] != (int)state.APPEAR && (int)last[0] != (int)state.LEAVE && hurtTime == 0 && hurtTap)
        {
            if(!safeStop) //First, we check to see if the player is under a low ceiling. If so, set last[1] to 1 to prevent the player from getting stuck once the hurt animation is complete.
            {
                last[1] = 1;
            }
            else
            {
                last[1] = 0;
            }
            changeAnim(state.HURT);
            velocity.y = 0;
            hurtTime = 96;
        }
    }

    public void jumpCheck()
    {
        //Player pressed jump while on the ground.
        if(jumpTap && IsOnFloor() && dirHold.y != 1 && safeStop)
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
        //Check to see if the player is on the ground. If now, swap to the jump/fall state.
        if(!IsOnFloor() && !forceIdle)
        {
            changeAnim(state.JUMP);
            if(!safeStop)
            {
                safeStop = true;
            }
        }

        if(IsOnFloor() && forceIdle) //Turn force idle off when on the ground to prevent issues with animations.
        {
            forceIdle = false;
        }
    }

    public void slideCheck()
    {
        //First, check to see if the player is up against a wall. This is to prevent the player from getting stuck.
        var walls = obsCheck.GetOverlappingBodies();
        bool onWall = false;

        if(walls.Count > 0)
        {
            onWall = true;
        }

        //If no wall detected, start slide functions.
        if(dirHold.y == 1 && jumpTap && slideDelay == 0 && !onWall)
        {
            changeAnim(state.SLIDE);
            if(sprite.FlipH)
            {
                lastXDir = -1;
            }
            else
            {
                lastXDir = 1;
            }
            slideDelay = 24;
        }
    }

    public void ladderCheck()
    {
        if(dirHold.y == -1 && overlap == 2 || dirTap.y == -1 && overlap == 1)
        {
            //The player pressed up while overlapping a ladder.
            //Snap the player into position and set velocity to 0;
            GlobalPosition = new Vector2(xSnap, GlobalPosition.y);
            xSpeed = 0;
            velocity = Vector2.Zero;
            //Change the state to climbing.
            changeAnim(state.CLIMB);
        }

        if(dirHold.y == 1 && below == 1)
        {
            //The player pressed down while standing on top of a ladder. As of right now, the player is able to climb down a ladder while sliding on top of it. To change this, simply
            //delete the slide state above where the game calls the ladderCheck function.
            //Snap the player into position and set velocity to 0;
            GlobalPosition = new Vector2(xSnap, GlobalPosition.y + 8);
            xSpeed = 0;
            velocity = Vector2.Zero;
            //Change the state to the top of a ladder.
            changeAnim(state.CLIMBTOP);
        }
    }

    public void weaponCheck()
    {
        //This example lacks weapons. I'll leave that up to you to get creative here. Instead, we'll swap textures to simulate a weapon being fired.
        if(fireTap && (int)last[0] != (int)state.BEAM && (int)last[0] != (int)state.APPEAR && (int)last[0] != (int)state.LEAVE && (int)last[0] != (int)state.SLIDE && (int)last[0] != (int)state.HURT)
        {
            shotDelay = 25;
        }
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

        //Save the last texture called.
        last[2] = which;
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
                stopX = false; //Same failsafe as above.
                break;

                case state.CLIMBTOP:
                anim.Play("climbTop");
                stopX = false; //Same failsafe as above.
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
