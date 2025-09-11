using UnityEngine;
using System.Collections;

namespace ithappy.Animals_FREE
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class AICreatureMover : MonoBehaviour
    {
        // --- AI Behavior ---
        [Header("AI Behavior")]
        [SerializeField]
        private float m_WanderRadius = 10f;
        [SerializeField]
        private float m_MinIdleTime = 1f;
        [SerializeField]
        private float m_MaxIdleTime = 5f;
        [SerializeField]
        private float m_StoppingDistance = 0.5f;

        private Vector3 m_WanderPoint;
        private Coroutine m_AiCoroutine;

        private enum AIState
        {
            Idle,
            Wandering
        }
        private AIState m_CurrentState = AIState.Idle;


        // --- Movement (from original script) ---
        [Header("Movement")]
        [SerializeField]
        private float m_WalkSpeed = 1f;
        [SerializeField]
        private float m_RunSpeed = 4f; // Can be used for 'chase' or 'flee' states
        [SerializeField, Range(0f, 360f)]
        private float m_RotateSpeed = 120f;
        [SerializeField]
        private float m_JumpHeight = 5f;

        // --- Animator (from original script) ---
        [Header("Animator")]
        [SerializeField]
        private string m_VerticalID = "Vert";
        [SerializeField]
        private string m_StateID = "State";
        [SerializeField]
        private LookWeight m_LookWeight = new(0.5f, 0.2f, 0.6f, 1f);

        // --- Component References ---
        private Transform m_Transform;
        private CharacterController m_Controller;
        private Animator m_Animator;

        // --- Handlers ---
        private MovementHandler m_Movement;
        private AnimationHandler m_Animation;

        // --- Internal State ---
        private Vector2 m_Axis; // Will be driven by AI now
        private Vector3 m_TargetLookPosition;
        private bool m_IsRun; // Can be controlled by AI state
        private bool m_IsMoving;

        private void OnValidate()
        {
            m_WalkSpeed = Mathf.Max(m_WalkSpeed, 0f);
            m_RunSpeed = Mathf.Max(m_RunSpeed, m_WalkSpeed);
            m_Movement?.SetStats(m_WalkSpeed, m_RunSpeed, m_RotateSpeed, m_JumpHeight, Space.World);
        }

        private void Awake()
        {
            m_Transform = transform;
            m_Controller = GetComponent<CharacterController>();
            m_Animator = GetComponent<Animator>();

            // AI always moves in world space
            m_Movement = new MovementHandler(m_Controller, m_Transform, m_WalkSpeed, m_RunSpeed, m_RotateSpeed, m_JumpHeight, Space.World);
            m_Animation = new AnimationHandler(m_Animator, m_VerticalID, m_StateID);
        }

        private void Start()
        {
            m_WanderPoint = m_Transform.position;
            m_TargetLookPosition = m_Transform.position + m_Transform.forward;
            // Start the AI behavior
            m_AiCoroutine = StartCoroutine(AIStateMachine());
        }

        private void Update()
        {
            // The core movement and animation logic remains the same
            m_Movement.Move(Time.deltaTime, in m_Axis, in m_TargetLookPosition, m_IsRun, m_IsMoving, out var animAxis);
            m_Animation.Animate(in animAxis, m_IsRun ? 1f : 0f, Time.deltaTime);
        }

        private void OnAnimatorIK()
        {
            // Creature can still look around while moving
            m_Animation.AnimateIK(in m_TargetLookPosition, m_LookWeight);
        }

        /// <summary>
        /// A simple state machine to control AI behavior.
        /// </summary>
        private IEnumerator AIStateMachine()
        {
            while (true)
            {
                switch (m_CurrentState)
                {
                    case AIState.Idle:
                        yield return StartCoroutine(IdleState());
                        break;
                    case AIState.Wandering:
                        yield return StartCoroutine(WanderState());
                        break;
                }
            }
        }

        private IEnumerator IdleState()
        {
            // Stop moving
            SetAIInput(Vector2.zero, false);

            // Wait for a random amount of time
            float idleTime = Random.Range(m_MinIdleTime, m_MaxIdleTime);
            yield return new WaitForSeconds(idleTime);

            // Transition to wandering
            m_CurrentState = AIState.Wandering;
        }

        private IEnumerator WanderState()
        {
            // Find a new random point to wander to
            FindNewWanderPoint();

            // Move towards the point until we reach it
            // We create temporary vectors to compare distance on the XZ plane only, ignoring height.
            Vector3 currentPositionXZ = new Vector3(m_Transform.position.x, 0, m_Transform.position.z);
            Vector3 wanderPointXZ = new Vector3(m_WanderPoint.x, 0, m_WanderPoint.z);

            while (Vector3.Distance(currentPositionXZ, wanderPointXZ) > m_StoppingDistance)
            {
                // For this mover, the axis represents forward movement.
                // We'll just move forward towards the target.
                SetAIInput(Vector2.up, false); // Vector2.up is (0,1), which means forward

                // We need to rotate towards the wander point
                m_TargetLookPosition = m_WanderPoint;

                // Update the XZ position for the next distance check in the loop
                currentPositionXZ.x = m_Transform.position.x;
                currentPositionXZ.z = m_Transform.position.z;

                yield return null;
            }

            // We've reached the destination, go back to idle
            m_CurrentState = AIState.Idle;
        }

        /// <summary>
        /// Finds a random point by raycasting down to find the ground.
        /// </summary>
        private void FindNewWanderPoint()
        {
            Vector3 randomDirection = Random.insideUnitSphere * m_WanderRadius;
            randomDirection += m_Transform.position;

            // Start the raycast from high above to ensure we hit varied terrain.
            Vector3 rayStart = new Vector3(randomDirection.x, m_Transform.position.y + 10f, randomDirection.z);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f))
            {
                // The new wander point is where the ray hit the ground.
                m_WanderPoint = hit.point;
            }
            else
            {
                // Fallback if we don't hit anything (e.g., wandering off a cliff).
                m_WanderPoint = m_Transform.position;
            }
        }


        /// <summary>
        /// AI version of SetInput. Drives the creature's movement.
        /// </summary>
        /// <param name="axis">The desired movement axis (Vector2.up for forward).</param>
        /// <param name="isRun">Should the creature run?</param>
        public void SetAIInput(in Vector2 axis, in bool isRun)
        {
            m_Axis = axis;
            m_IsRun = isRun;

            if (m_Axis.sqrMagnitude < Mathf.Epsilon)
            {
                m_Axis = Vector2.zero;
                m_IsMoving = false;
            }
            else
            {
                m_Axis = Vector2.ClampMagnitude(m_Axis, 1f);
                m_IsMoving = true;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > m_Controller.stepOffset)
            {
                m_Movement.SetSurface(hit.normal);
            }
        }

        // --- Structs and Handlers (Mostly unchanged from original) ---

        [System.Serializable]
        private struct LookWeight
        {
            public float weight;
            public float body;
            public float head;
            public float eyes;

            public LookWeight(float weight, float body, float head, float eyes)
            {
                this.weight = weight;
                this.body = body;
                this.head = head;
                this.eyes = eyes;
            }
        }

        #region Handlers
        private class MovementHandler
        {
            private readonly CharacterController m_Controller;
            private readonly Transform m_Transform;
            private float m_WalkSpeed;
            private float m_RunSpeed;
            private float m_RotateSpeed;
            private readonly Space m_Space;
            private Vector3 m_GravityAcelleration = Physics.gravity;
            private Vector3 m_Normal;

            public MovementHandler(CharacterController controller, Transform transform, float walkSpeed, float runSpeed, float rotateSpeed, float jumpHeight, Space space)
            {
                m_Controller = controller;
                m_Transform = transform;
                SetStats(walkSpeed, runSpeed, rotateSpeed, jumpHeight, space);
                m_Space = space; // This is set here
            }

            public void SetStats(float walkSpeed, float runSpeed, float rotateSpeed, float jumpHeight, Space space)
            {
                m_WalkSpeed = walkSpeed;
                m_RunSpeed = runSpeed;
                m_RotateSpeed = rotateSpeed;
            }

            public void SetSurface(in Vector3 normal)
            {
                m_Normal = normal;
            }

            public void Move(float deltaTime, in Vector2 axis, in Vector3 target, bool isRun, bool isMoving, out Vector2 animAxis)
            {
                // Simplified rotation for AI: Directly look towards the target
                Vector3 lookDirection = target - m_Transform.position;
                lookDirection.y = 0;

                if (lookDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                    m_Transform.rotation = Quaternion.Slerp(m_Transform.rotation, targetRotation, m_RotateSpeed * deltaTime);
                }

                // Movement logic
                Vector3 forward = m_Transform.forward;
                Vector3 right = m_Transform.right;

                Vector3 movement = axis.x * right + axis.y * forward;
                movement = Vector3.ProjectOnPlane(movement, m_Normal);

                CaculateGravity(deltaTime);
                Displace(deltaTime, in movement, isRun);

                GenAnimationAxis(in movement, out animAxis);
            }

            private void Displace(float deltaTime, in Vector3 movement, bool isRun)
            {
                Vector3 displacement = (isRun ? m_RunSpeed : m_WalkSpeed) * movement;
                displacement += m_GravityAcelleration;
                displacement *= deltaTime;
                m_Controller.Move(displacement);
            }

            private void CaculateGravity(float deltaTime)
            {
                if (m_Controller.isGrounded)
                {
                    m_GravityAcelleration = Physics.gravity * 0.1f; // A little gravity to stick to ground
                }
                else
                {
                    m_GravityAcelleration += Physics.gravity * deltaTime;
                }
            }

            private void GenAnimationAxis(in Vector3 movement, out Vector2 animAxis)
            {
                // Animation axis is simpler now, as AI just moves forward.
                animAxis = new Vector2(0, movement.magnitude);
            }
        }

        private class AnimationHandler
        {
            private readonly Animator m_Animator;
            private readonly int m_VerticalHash;
            private readonly int m_StateHash;
            private readonly float k_InputFlow = 4.5f;
            private float m_FlowState;
            private float m_FlowVertical;

            public AnimationHandler(Animator animator, string verticalID, string stateID)
            {
                m_Animator = animator;
                m_VerticalHash = Animator.StringToHash(verticalID);
                m_StateHash = Animator.StringToHash(stateID);
            }

            public void Animate(in Vector2 axis, float state, float deltaTime)
            {
                // Smooth the vertical animation parameter
                m_FlowVertical = Mathf.Lerp(m_FlowVertical, axis.y, k_InputFlow * deltaTime);
                m_FlowState = Mathf.Lerp(m_FlowState, state, k_InputFlow * deltaTime);

                m_Animator.SetFloat(m_VerticalHash, m_FlowVertical);
                m_Animator.SetFloat(m_StateHash, m_FlowState);
            }

            public void AnimateIK(in Vector3 target, in LookWeight lookWeight)
            {
                m_Animator.SetLookAtPosition(target);
                m_Animator.SetLookAtWeight(lookWeight.weight, lookWeight.body, lookWeight.head, lookWeight.eyes);

            }
        }
        #endregion
    }
}

