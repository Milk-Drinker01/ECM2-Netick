using ECM2;
using Netick;
using Netick.Unity;
using UnityEngine;

public class PlayerControllerNetick : NetworkBehaviour
{
    public struct ECM2Input : INetworkInput
    {
        public float horizontal;
        public float vertical;
        public bool Jump;
    }

    public struct ECM2Data
    {
        //Position and Rotation removed here because we are using NetworkTransform to sync these instead.
        //public Vector3 Position;
        //public Quaternion Rotation;
        public Vector3 Velocity;
        public bool ConstrainToGround;
        public float UnConstrainTime;
        public bool HitGround;
        public bool IsWalkable;

        //public ECM2Data(Vector3 position, Quaternion rotation, Vector3 velocity, bool constrainedToGround, float unconstrainedTime, bool hitGround, bool isWalkable)
        public ECM2Data(Vector3 velocity, bool constrainedToGround, float unconstrainedTime, bool hitGround, bool isWalkable)
        {
            //this.Position = position;
            //this.Rotation = rotation;

            this.Velocity = velocity;

            this.ConstrainToGround = constrainedToGround;
            this.UnConstrainTime = unconstrainedTime;

            this.HitGround = hitGround;
            this.IsWalkable = isWalkable;
        }
    }

    [Networked] public ECM2Data Data { get; set; }

    #region EDITOR EXPOSED FIELDS
    public float rotationRate = 540.0f;

    public float maxSpeed = 5;

    public float acceleration = 20.0f;
    public float deceleration = 20.0f;

    public float groundFriction = 8.0f;
    public float airFriction = 0.5f;

    public float jumpImpulse = 6.5f;

    [Range(0.0f, 1.0f)]
    public float airControl = 0.3f;

    public Vector3 gravity = Vector3.down * 9.81f;
    #endregion

    private CharacterMovementNetick characterMovement { get; set; }

    public override void NetworkAwake()
    {
        characterMovement = GetComponent<CharacterMovementNetick>();
        characterMovement.SandboxPhysicsScene = Sandbox.Physics;
    }

    public override void NetworkUpdate()
    {
        if (!IsInputSource || !Sandbox.InputEnabled)
            return;

        var networkInput = Sandbox.GetInput<ECM2Input>();

        networkInput.horizontal = Input.GetAxisRaw("Horizontal");
        networkInput.vertical = Input.GetAxisRaw("Vertical");

        networkInput.Jump |= Input.GetKeyDown(KeyCode.Space);

        Sandbox.SetInput<ECM2Input>(networkInput);
    }

    // rollback client state
    public override void NetcodeIntoGameEngine()
    {
        characterMovement.SetState(
                transform.position,
                transform.rotation,
                Data.Velocity,
                Data.ConstrainToGround,
                Data.UnConstrainTime,
                Data.HitGround,
                Data.IsWalkable);
    }

    // simulate/step the controller
    public override void NetworkFixedUpdate()
    {
        Vector3 moveDirection = Vector3.zero;
        Vector3 desiredVelocity = Vector3.zero;
        if (FetchInput(out ECM2Input md))
        {
            // Jump
            if (md.Jump && characterMovement.isGrounded)
            {
                characterMovement.PauseGroundConstraint();
                characterMovement.velocity.y = Mathf.Max(characterMovement.velocity.y, jumpImpulse);
            }

            // Movement
            moveDirection = Vector3.right * md.horizontal + Vector3.forward * md.vertical;
            moveDirection = Vector3.ClampMagnitude(moveDirection, 1.0f);
            desiredVelocity = moveDirection * maxSpeed;
        }

        if (Sandbox.IsServer || IsPredicted)
        {
            float actualAcceleration = characterMovement.isGrounded ? acceleration : acceleration * airControl;
            float actualDeceleration = characterMovement.isGrounded ? deceleration : 0.0f;

            float actualFriction = characterMovement.isGrounded ? groundFriction : airFriction;

            float deltaTime = Sandbox.ScaledFixedDeltaTime;
            characterMovement.RotateTowards(moveDirection, rotationRate * deltaTime);
            characterMovement.SimpleMove(desiredVelocity, maxSpeed, actualAcceleration, actualDeceleration,
                actualFriction, actualFriction, gravity, true, deltaTime);

            transform.position = characterMovement.position;
            transform.rotation = characterMovement.rotation;
        }
    }

    //at the end of the tick, set our character state
    public override void GameEngineIntoNetcode()
    {
        Data = new ECM2Data(
                    characterMovement.velocity,
                    characterMovement.constrainToGround,
                    characterMovement.unconstrainedTimer,
                    characterMovement.currentGround.hitGround,
                    characterMovement.currentGround.isWalkable
                );
    }
}
