using UnityEngine;

/// <summary>
/// Third-person chase camera: sits behind the cart, swings round corners with lag, trails further
/// back and widens the view at speed, and shakes on crashes. Doesn't need to be parented to the cart.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CartCamera : MonoBehaviour
{
    [Tooltip("Cart to follow. Leave empty to use the Drivable this camera is parented under, or the first one in the scene.")]
    public Drivable cart;
    [Tooltip("How far behind the cart the camera sits when standing still.")]
    public float distance = 6f;
    [Tooltip("Extra distance added at top speed.")]
    public float extraDistanceAtTopSpeed = 4f;
    [Tooltip("How high above the cart the camera sits.")]
    public float height = 3f;
    [Tooltip("Point above the cart's centre the camera looks at.")]
    public float lookAtHeight = 1f;
    [Tooltip("Higher = camera swings round behind the cart faster when turning. Lower = lazier, smoother.")]
    public float rotationSharpness = 2.5f;
    [Tooltip("Seconds the camera position takes to catch up. Higher = floatier.")]
    public float positionSmoothTime = 0.15f;
    [Tooltip("Field of view added at top speed.")]
    public float extraFovAtTopSpeed = 15f;
    public float maxShake = 0.4f;
    public float shakeFadeSpeed = 3f;

    Camera cam;
    float yaw, baseFov, shake, speed01;
    Vector3 pos, vel;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (!cart) cart = GetComponentInParent<Drivable>();
        if (!cart) cart = FindFirstObjectByType<Drivable>();
        if (!cart)
        {
            Debug.LogWarning("CartCamera found no Drivable to follow.", this);
            enabled = false;
            return;
        }

        yaw = cart.transform.eulerAngles.y;
        baseFov = cam.fieldOfView;
        pos = transform.position;
        transform.SetParent(null); // detach so we can lag behind instead of being glued on
        cart.Crashed += strength => shake = Mathf.Max(shake, strength);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Transform t = cart.transform;

        // Smoothed speed so the pull-back doesn't twitch on bumps
        speed01 = Mathf.Lerp(speed01, Mathf.Clamp01(Mathf.Abs(cart.Speed) / cart.maxSpeed), 1f - Mathf.Exp(-2f * dt));

        yaw = Mathf.LerpAngle(yaw, t.eulerAngles.y, 1f - Mathf.Exp(-rotationSharpness * dt));
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 target = t.position + Vector3.up * lookAtHeight;
        Vector3 desired = t.position + turn * new Vector3(0f, height, -(distance + extraDistanceAtTopSpeed * speed01));
        pos = Vector3.SmoothDamp(pos, desired, ref vel, positionSmoothTime);

        Vector3 shakeOffset = Random.insideUnitSphere * shake * maxShake;
        transform.SetPositionAndRotation(pos + shakeOffset, Quaternion.LookRotation(target - pos));
        shake = Mathf.Max(0f, shake - shakeFadeSpeed * dt);

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + extraFovAtTopSpeed * speed01, 1f - Mathf.Exp(-3f * dt));
    }
}
