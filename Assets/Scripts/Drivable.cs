using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class Drivable : MonoBehaviour
{
    [Header("Engine")]
    public float maxSpeed = 20f;
    public float maxReverseSpeed = 6f;
    public float acceleration = 1f;
    public float weight = 2f;
    [Tooltip("How fast S slows the cart down while moving forward.")]
    public float brakeStrength = 25f;
    [Tooltip("Slowdown when no key is pressed, as a fraction of acceleration. Heavier carts roll further.")]
    public float coastFactor = 0.4f;

    [Header("Steering")]
    public float steering = 90f;
    [Tooltip("Speed at which steering reaches full strength. Slower = turns less.")]
    public float fullSteeringSpeed = 8f;
    [Tooltip("How quickly the cart's velocity follows where it points. Lower = more sliding.")]
    public float grip = 10f;
    public Transform leftPivot;
    public Transform rightPivot;

    [Header("Mud")]
    [Tooltip("Ground with this physics material counts as mud.")]
    public PhysicsMaterial mudMaterial;
    public float mudSpeedMultiplier = 0.5f;
    public float mudGrip = 2f;

    [Header("Crashes")]
    [Tooltip("Speed kept after hitting a wall (0.3 = 30%).")]
    public float crashSpeedKept = 0.3f;

    [Header("Respawn (R key, or falling)")]
    [Tooltip("The road spline. Respawn puts the cart back on the nearest point of it.")]
    public SplineContainer track;
    public float fallResetHeight = -20f;

    [Header("Feedback (all optional)")]
    public TextMeshProUGUI displaySpeedText;
    public AudioSource engineSound;
    public AudioSource crashSound;
    public ParticleSystem dust;

    public float Speed => speed;
    public event System.Action<float> Crashed; // strength 0..1, used by CartCamera for shake

    Rigidbody rb;
    float speed;
    bool grounded, onMud;
    Vector3 startPos;
    Quaternion startRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;                          // only our steering turns the cart, so it can't flip
        rb.interpolation = RigidbodyInterpolation.Interpolate; // smooth camera follow
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // no tunnelling through thin obstacles
        rb.sleepThreshold = 0f; // a sleeping body stops OnCollisionStay, so a parked cart would think it's airborne

        // The script sets the cart's speed itself; physics friction would only fight it and make starts sticky
        var noFriction = new PhysicsMaterial("Cart (no friction)")
        {
            dynamicFriction = 0f, staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
        };
        foreach (var col in GetComponentsInChildren<Collider>()) col.sharedMaterial = noFriction;
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.rKey.wasPressedThisFrame || transform.position.y < fallResetHeight) Respawn();

        float speed01 = Mathf.Abs(speed) / maxSpeed;
        if (displaySpeedText) displaySpeedText.text = Mathf.RoundToInt(Mathf.Abs(speed) * 3.6f) + " km/h";
        if (engineSound) engineSound.pitch = Mathf.Lerp(0.8f, 2f, speed01);
        if (dust)
        {
            var emission = dust.emission;
            emission.rateOverTime = grounded ? speed01 * 40f : 0f;
        }
    }

    void FixedUpdate()
    {
        float dt = Time.deltaTime;
        var kb = Keyboard.current;
        bool gas = kb != null && kb.wKey.isPressed;
        bool back = kb != null && kb.sKey.isPressed;
        float top = onMud ? maxSpeed * mudSpeedMultiplier : maxSpeed;

        // 60: acceleration used to be added once per frame; this keeps that tuning at 60 fps on any frame rate
        float accel = acceleration / weight * 60f * dt;

        if (gas && speed < 0) speed = Mathf.Min(speed + brakeStrength * dt, 0f);
        else if (gas) speed = Mathf.Min(speed + accel, top);
        else if (back && speed > 0) speed = Mathf.Max(speed - brakeStrength * dt, 0f);
        else if (back) speed = Mathf.Max(speed - accel, -maxReverseSpeed);
        else speed = Mathf.MoveTowards(speed, 0f, accel * coastFactor);

        if (speed > top) speed = Mathf.MoveTowards(speed, top, brakeStrength * dt); // drove into mud

        // Steering: weaker when slow, flipped in reverse like a real car
        float steer = (kb != null && kb.dKey.isPressed ? 1f : 0f) - (kb != null && kb.aKey.isPressed ? 1f : 0f);
        if (steer != 0f && speed != 0f)
        {
            float amount = steer * steering * Mathf.Clamp01(Mathf.Abs(speed) / fullSteeringSpeed) * Mathf.Sign(speed) * dt;
            Transform pivot = steer < 0 ? leftPivot : rightPivot;
            Vector3 p = pivot ? pivot.position : rb.position;
            Quaternion turn = Quaternion.AngleAxis(amount, Vector3.up);
            rb.MovePosition(p + turn * (rb.position - p));
            rb.MoveRotation(turn * rb.rotation);
        }

        // Drive: horizontal velocity eases toward where the cart points; gravity keeps the vertical part
        if (grounded)
        {
            Vector3 v = rb.linearVelocity;
            Vector3 target = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * speed;
            Vector3 flat = Vector3.Lerp(new Vector3(v.x, 0f, v.z), target, (onMud ? mudGrip : grip) * dt);
            rb.linearVelocity = new Vector3(flat.x, v.y, flat.z);
        }

        grounded = onMud = false; // refreshed by OnCollisionStay every physics step
    }

    void OnCollisionStay(Collision c)
    {
        for (int i = 0; i < c.contactCount; i++)
        {
            if (c.GetContact(i).normal.y > 0.5f)
            {
                grounded = true;
                if (mudMaterial && c.collider.sharedMaterial == mudMaterial) onMud = true;
                return;
            }
        }
    }

    void OnCollisionEnter(Collision c)
    {
        // Ignore landing on the ground; only walls/obstacles count as crashes
        if (c.GetContact(0).normal.y > 0.5f) return;
        float strength = Mathf.Clamp01(c.relativeVelocity.magnitude / maxSpeed);
        if (strength < 0.1f) return;

        speed *= crashSpeedKept;
        if (crashSound) crashSound.Play();
        Crashed?.Invoke(strength);
    }

    public void Respawn()
    {
        Vector3 pos = startPos;
        Quaternion rot = startRot;
        if (track)
        {
            float3 local = track.transform.InverseTransformPoint(transform.position);
            SplineUtility.GetNearestPoint(track.Spline, local, out _, out float t);
            pos = (Vector3)track.EvaluatePosition(t) + Vector3.up;
            Vector3 fwd = Vector3.ProjectOnPlane((Vector3)track.EvaluateTangent(t), Vector3.up);
            if (fwd.sqrMagnitude > 0.001f) rot = Quaternion.LookRotation(fwd);
        }

        speed = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.position = pos;
        rb.rotation = rot;
        transform.SetPositionAndRotation(pos, rot);
    }
}
