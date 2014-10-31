﻿#region File Description
//-----------------------------------------------------------------------------
// Enemy.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

namespace Platformer
{
    /// <summary>
    /// Facing direction along the X axis.
    /// </summary>
    enum FaceDirection
    {
        Left = -1,
        Right = 1,
    }

    /// <summary>
    /// A monster who is impeding the progress of our fearless adventurer.
    /// </summary>
    class Enemy: GameCharacter
    {
        /// <summary>
        /// returns line of sight to player
        /// </summary>
        public Vector2 EnemyLineOfSight
        {
            get { return Level.ActiveHero.Position - Position; }
        }

        /// <summary>
        /// Constructs a new Enemy.
        /// </summary>
        public Enemy(Level level, Vector2 initialPosition): base(level, initialPosition)
       { 
            this.state = EnemyState.Search;
            health = MaxHealth;
            Reset(initialPosition);
        }

        /// <summary>
        /// Loads a particular enemy sprite sheet and sounds.
        /// </summary>
        protected override void LoadContent()
        {
            // Load the activeHero's default weapon
            base.LoadContent();
            // Load animations.
            string spriteSet = "Sprites/" + enemyType + "/";
            runAnimation = new Animation(Level.Content.Load<Texture2D>(spriteSet + "Run"), 0.1f, true);
            idleAnimation = new Animation(Level.Content.Load<Texture2D>(spriteSet + "Idle"), 0.15f, true);
            dieAnimation = new Animation(Level.Content.Load<Texture2D>(spriteSet + "Die"), 0.07f, false);
            explosionAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/explosion"), 0.1f, false); //false means the animation is not going to loop
            sprite.LoadAnimation(idleAnimation);
            // Load sounds.
            killedSound = Level.Content.Load<SoundEffect>("Sounds/MonsterKilled");
            // Temporary hurt sound. We probably want to use something different in the future.
            hurtSound = killedSound;
            // Load enemy default weapon
            weapon = new Gun(Level.Content.Load<Texture2D>("Sprites/Player/Arm_Gun"), this);
        }


        /// <summary>
        /// Updates the ai and position of enemy.
        /// TODO We should refactor this to use the state pattern 
        /// design pattern: http://sourcemaking.com/design_patterns/state
        /// </summary>
        public override void Update(GameTime gameTime, InputHandler gameInputs)
        {
            // is this a necessary/smart call? If called we could get some re-use
            // out of the physics/collision engine code, but we need to refactor
            // determineAnimation
            // base.Update(GameTime gameTime, InputHandler gameInputs)
            if (!IsAlive)
                return;
            // update enemy AI with the line of sight to the player.
            UpdateAI(gameTime); 
        }

        protected virtual void UpdateAI(GameTime gameTime)
        {
            // Updates the enemy AI state machine
            UpdateState(gameTime); //changes the state if need be
            //These different methods define the enemy's actions for this frame
            switch (state)
            {
                case EnemyState.Search:
                    Search(gameTime);
                    break;
                case EnemyState.Track:
                    Track(gameTime);
                    break;
                case EnemyState.Attack:
                    Attack(gameTime);
                    break;
                case EnemyState.Kamikaze:
                    Kamikaze(gameTime);
                    break;
            }
            
        }

        /// <summary>
        /// changes the state of the enemy depending on the distance to the player and health
        /// </summary>
        protected virtual void UpdateState(GameTime gameTime)
        {
            switch (state)
            {
                case EnemyState.Search:
                    //if health is lower than threshold then kamikaze
                    if (health <= MaxHealth * KamikazeThresholdPercent)
                        state = EnemyState.Kamikaze;
                    else if (EnemyLineOfSight.X * (int)direction >= 0) //make sure enemy is facing the right direction
                    {
                        if (Math.Abs(EnemyLineOfSight.X) <= MaxAttackDistance)// player is in attacking distance then attack
                            state = EnemyState.Attack;
                        else if (Math.Abs(EnemyLineOfSight.X) <= MinTrackDistance)//or at least in tracking distance then track
                            state = EnemyState.Track;
                    }
                    break;
                case EnemyState.Track:
                    //if health is lower than threshold than kamikaze
                    if (health <= MaxHealth * KamikazeThresholdPercent)
                        state = EnemyState.Kamikaze;
                    else if (Math.Abs(EnemyLineOfSight.X) <= MaxAttackDistance)// player is in attacking distance then attack
                        state = EnemyState.Attack;
                    Track(gameTime);
                    break;
                case EnemyState.Attack:
                    // if health is lower than threshold than kamikaze
                    if (health <= MaxHealth * KamikazeThresholdPercent)
                        state = EnemyState.Kamikaze;
                    else if (Math.Abs(EnemyLineOfSight.X) > MaxAttackDistance)// player moved outside of attacking range then track
                        state = EnemyState.Track;
                    break;
                case EnemyState.Kamikaze:
                    //nothing to change to
                    break;
            }
        }

        /// <summary>
        /// Searches by pacing back and forth along a platform, waiting at either end.
        /// </summary>
        protected void Search(GameTime gameTime)
        {
            color = Color.White; //for debugging no change if searching

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Calculate tile position based on the side we are walking towards.
            float posX = Position.X + localBounds.Width / 2 * (int)direction;
            int tileX = (int)Math.Floor(posX / Level.TileWidth) - (int)direction;
            int tileY = (int)Math.Floor(Position.Y / Level.TileHeight);

            if (waitTime > 0)
            {
                // Wait for some amount of time.
                waitTime = Math.Max(0.0f, waitTime - (float)gameTime.ElapsedGameTime.TotalSeconds);
                if (waitTime <= 0.0f)
                {
                    // Then turn around.
                    direction = (FaceDirection)(-(int)direction);
                }
            }
            else
            {
                // If we are about to run into a wall or off a cliff, start waiting.
                if (Level.GetCollision(tileX + (int)direction, tileY - 1) == TileCollision.Impassable ||
                    Level.GetCollision(tileX + (int)direction, tileY) == TileCollision.Passable)
                {
                    waitTime = MaxWaitTime;
                }
                else
                {
                    // Move in the current direction.
                    Vector2 velocity = new Vector2((int)direction * MoveSpeed * elapsed, 0.0f);
                    Position = Position + velocity;
                }
            }
        }

        /// <summary>
        /// Moves toward player, is still too far away to attack
        /// </summary>
        protected void Track(GameTime gameTime)
        {
            color = Color.Yellow;//for debugging yellow if tracking

            if (EnemyLineOfSight.X * (int)direction < 0) //make sure enemy is facing the right direction
                direction = (FaceDirection)(-(int)direction); //if not turn around

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // Calculate tile position based on the side we are walking towards.
            float posX = Position.X + localBounds.Width / 2 * (int)direction;
            int tileX = (int)Math.Floor(posX / Level.TileWidth) - (int)direction;
            int tileY = (int)Math.Floor(Position.Y / Level.TileHeight);

            // If we are about to run into a wall or off a cliff, then stop.
            if (Level.GetCollision(tileX + (int)direction, tileY - 1) != TileCollision.Impassable &&
                    Level.GetCollision(tileX + (int)direction, tileY) != TileCollision.Passable)
            {
                // Else Move in the current direction.
                Vector2 velocity = new Vector2((int)direction * MoveSpeed * elapsed, 0.0f);
                Position = Position + velocity;
            }
        }

        /// <summary>
        /// Attacking player, by shooting
        /// </summary>
        protected void Attack(GameTime gameTime)
        {
            color = Color.Orange;//for debugging orange if attacking

            if (EnemyLineOfSight.X * (int)direction < 0) //make sure enemy is facing the right direction
                direction = (FaceDirection)(-(int)direction); //if not turn around

            //-------------SHOOTING HERE----------------------- maybe some movement too

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        /// <summary>
        /// Charges at player
        /// </summary>
        protected void Kamikaze(GameTime gameTime)
        {
            color = Color.Red;//for debugging red if kamikaze

            if (EnemyLineOfSight.X * (int)direction < 0) //make sure enemy is facing the right direction
                direction = (FaceDirection)(-(int)direction); //if not turn around

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            // move in the current direction.
            Vector2 velocity = new Vector2((int)direction * MoveSpeed * 2 * elapsed, 0.0f); //twice as fast
            Position = Position + velocity;
        }


        /// <summary>
        /// Draws the animated enemy.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            // Stop running when the game is paused or before turning around.
            if (!IsAlive)
            {
                //sprite.LoadAnimation(explosionAnimation); //doesn't work for some reason
                sprite.LoadAnimation(dieAnimation);//then play the enemy dying
            }
            //if player is not alive or if player hasn't reached the exit, or if the time
            //remaining is 0, or if waiting time is greater than 0
            //then the idle animation for the enemies is playing
            else if (!Level.ActiveHero.IsAlive ||
                      Level.ReachedExit ||
                      Level.TimeRemaining == TimeSpan.Zero ||
                      waitTime > 0)
            {
                sprite.LoadAnimation(idleAnimation);
            }
            else
            {
                //if none of the above, then enemies are running
                sprite.LoadAnimation(runAnimation);
            }

            // Draw facing the way the enemy is moving.
            SpriteEffects flip = direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sprite.Draw(gameTime, spriteBatch, Position, flip, color);
        }

        /// <summary>
        /// enemy dies
        /// </summary>
        public override void OnKilled(GameCharacter killedBy)
        {
            base.OnKilled(killedBy);
            SpawnRandomConsumable();
        }

        /// <summary>
        /// spawns a random consumable at the place the enemy dies
        /// </summary>
        protected void SpawnRandomConsumable()
        {
            Point point;
            point.Y = BoundingRectangle.Top + BoundingRectangle.Height / 3;
            point.X = BoundingRectangle.Center.X;

            Random random = new Random();
            int rand = random.Next(100);
            if (rand < 30)
                Level.SpawnConsumable(point.X, point.Y, "HealthConsumable");
            else if (rand > 90)
                Level.SpawnConsumable(point.X, point.Y, "PowerUp");
        }

        /// <summary>
        /// Enemy explosion animation.
        /// </summary>
        protected Animation explosionAnimation;

        /// <summary>
        /// The speed at which this enemy moves along the X axis.
        /// </summary>
        protected float MoveSpeed = 40.0F;

        /// <summary>
        /// max health of enemy
        /// </summary>
        protected int MaxHealth = 10;

        /// <summary>
        /// if player is within this distance than transition to tracking state from searching state
        /// </summary>
        protected float MinTrackDistance = 500.0F;

        /// <summary>
        /// if player is within this distance than you can attack
        /// </summary>
        protected float MaxAttackDistance = 200.0F;

        /// <summary>
        /// if health is less than this percent of max health than kamikaze
        /// </summary>
        protected float KamikazeThresholdPercent = 0.4F;
        
        protected EnemyState state;

        protected string enemyType;

        /// <summary>
        /// The direction this enemy is facing and moving along the X axis.
        /// </summary>
        private FaceDirection direction = FaceDirection.Left;

        /// <summary>
        /// How long this enemy has been waiting before turning around.
        /// </summary>
        private float waitTime;

        /// <summary>
        /// How long to wait before turning around.
        /// </summary>
        private const float MaxWaitTime = 0.5f;
    }
}
