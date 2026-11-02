using UnityEngine;

/// <summary>
/// Put on the camera that is a child of the cart. Third-person chase camera: sits behind the cart,
/// swings round corners with a little lag, widens the view at speed and shakes on crashes.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CartCamera : MonoBehaviour
{
    [Tooltip("How far behind the cart the camera sits.")]
    public float distance = 7f;
    [Tooltip("How high above the cart the camera sits.")]
    public float height = 3f;
    [Tooltip("Point above the cart's centre the camera looks at.")]
    public float lookAtHeight = 1f;
    [Tooltip("Higher = camera swings round behind the cart faster when turning.")]
    public float rotationSharpness = 5f;
    [Tooltip("Field of view added at top speed.")]
    public float extraFovAtTopSpeed = 15f;
    public float maxShake = 0.4f;
    public float shakeFadeSpeed = 3f;

    Drivable cart;
    Camera cam;
    float yaw, baseFov, shake;

    void Start()
    {
        cart = GetComponentInParent<Drivable>();
        cam = GetComponent<Camera>();
        if (!cart || cart.gameObject == gameObject)
        {
            Debug.LogWarning("CartCamera goes on a camera that is a CHILD of the cart (e.g. Cube POV), not on the cart itself.", this);
            enabled = false;
            return;
        }

        yaw = cart.transform.eulerAngles.y;
        baseFov = cam.fieldOfView;
        transform.SetParent(null); // detach so we can lag behind instead of being glued on
        cart.Crashed += strength => shake = Mathf.Max(shake, strength);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Transform t = cart.transform;

        // Only the turn is smoothed; distance to the cart stays fixed so it never drifts or cuts into it
        yaw = Mathf.LerpAngle(yaw, t.eulerAngles.y, 1f - Mathf.Exp(-rotationSharpness * dt));
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        Vector3 target = t.position + Vector3.up * lookAtHeight;
        Vector3 pos = t.position + turn * new Vector3(0f, height, -distance);
        Vector3 shakeOffset = Random.insideUnitSphere * shake * maxShake;
        transform.SetPositionAndRotation(pos + shakeOffset, Quaternion.LookRotation(target - pos));
        shake = Mathf.Max(0f, shake - shakeFadeSpeed * dt);

        float targetFov = baseFov + extraFovAtTopSpeed * Mathf.Abs(cart.Speed) / cart.maxSpeed;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-3f * dt));
    }
}
