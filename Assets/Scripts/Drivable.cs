//using UnityEngine;
//using UnityEngine.InputSystem;
//using TMPro;

//public class Drivable : MonoBehaviour
//{
//    public float maxSpeed = 20f;
//    public float steering = 90f;
//    public float acceleration = 1f;
//    public float weight = 2f;
//    public Transform leftPivot;
//    public Transform rightPivot;

//    public TextMeshProUGUI displaySpeedText;

//    private float speed = 0;
//    private float deceleration = 0f;

//    void Start(){
//        deceleration = acceleration / weight;
//    }

//    void Update(){
//        displaySpeedText.text = "Current speed: " + speed.ToString();
//        if(Keyboard.current == null) return;

//        if(Keyboard.current.wKey.isPressed){
//            speed = speed < maxSpeed ? speed + acceleration/weight : speed;
//            transform.Translate(Vector3.forward * speed * Time.deltaTime);
//        } else if(speed > 0){
//            //speed -= (speed - deceleration)/5;
//            //if (speed < 0.5) speed = 0;
//            //transform.Translate(Vector3.forward * speed * Time.deltaTime);

//            speed -= (deceleration * (maxSpeed - speed + 1f) * Time.deltaTime) * 1.1f;
//            speed = Mathf.Max(speed, 0f);
//            transform.Translate(Vector3.forward * speed * Time.deltaTime);
//        }

//        if(speed > 0 && Keyboard.current.aKey.isPressed){
//            transform.RotateAround(leftPivot.position, transform.up, -steering * Time.deltaTime);
//        }

//        if(speed > 0 && Keyboard.current.dKey.isPressed){
//            transform.RotateAround(rightPivot.position, transform.up, steering * Time.deltaTime);
//        }
//    }
//}



using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Drivable : MonoBehaviour
{
    [Header("Driving")]
    [SerializeField] private float maxSpeed = 20f;
    [SerializeField] private float maxReverseSpeed = 6f;
    [SerializeField] private float acceleration = 10f;  // units per second²
    [SerializeField] private float weight = 2f;         // higher = slower to accelerate
    [SerializeField] private float coastDeceleration = 4f;
    [SerializeField] private float brakeDeceleration = 15f;

    [Header("Steering")]
    [SerializeField] private float steering = 90f;      // degrees per second
    [SerializeField] private Transform leftPivot;
    [SerializeField] private Transform rightPivot;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI displaySpeedText;

    private float speed; // negative = reversing

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;
        HandleThrottle(kb, dt);
        HandleSteering(kb, dt);

        transform.Translate(Vector3.forward * speed * dt);

        if (displaySpeedText != null)
            displaySpeedText.text = $"Current speed: {Mathf.Abs(speed) * 3.6f:F0} km/h";
    }

    void HandleThrottle(Keyboard kb, float dt)
    {
        bool gas = kb.wKey.isPressed;
        bool brake = kb.sKey.isPressed;

        if (gas && !brake)
            speed = Mathf.MoveTowards(speed, maxSpeed, (acceleration / weight) * dt);
        else if (brake)
            // brakes hard if moving forward, then reverses slowly
            speed = Mathf.MoveTowards(speed, speed > 0 ? 0f : -maxReverseSpeed,
                                      (speed > 0 ? brakeDeceleration : acceleration / weight) * dt);
        else
            speed = Mathf.MoveTowards(speed, 0f, coastDeceleration * dt);
    }

    void HandleSteering(Keyboard kb, float dt)
    {
        float steerInput = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        if (steerInput == 0f || Mathf.Abs(speed) < 0.1f) return;

        // Less steering at high speed, and flip direction when reversing
        float speedFactor = Mathf.Lerp(1f, 0.4f, Mathf.Abs(speed) / maxSpeed);
        float direction = Mathf.Sign(speed);
        Transform pivot = steerInput < 0 ? leftPivot : rightPivot;

        transform.RotateAround(pivot.position, transform.up,
                               steerInput * direction * steering * speedFactor * dt);
    }
}