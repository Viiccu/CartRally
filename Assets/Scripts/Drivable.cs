using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class Drivable : MonoBehaviour
{
    public float maxSpeed = 20f;
    public float steering = 90f;
    public float acceleration = 1f;
    public float weight = 2f;
    public Transform leftPivot;
    public Transform rightPivot;

    public TextMeshProUGUI displaySpeedText;

    private float speed = 0;
    private float deceleration = 0f;
    
    void Start(){
        deceleration = acceleration / weight;
    }

    void Update(){
        displaySpeedText.text = "Current speed: " + speed.ToString();
        if(Keyboard.current == null) return;

        if(Keyboard.current.wKey.isPressed){
            speed = speed < maxSpeed ? speed + acceleration/weight : speed;
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        } else if(speed > 0){
            //speed -= (speed - deceleration)/5;
            //if (speed < 0.5) speed = 0;
            //transform.Translate(Vector3.forward * speed * Time.deltaTime);

            speed -= (deceleration * (maxSpeed - speed + 1f) * Time.deltaTime) * 1.1f;
            speed = Mathf.Max(speed, 0f);
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
            
        if(speed > 0 && Keyboard.current.aKey.isPressed){
            transform.RotateAround(leftPivot.position, transform.up, -steering * Time.deltaTime);
        }

        if(speed > 0 && Keyboard.current.dKey.isPressed){
            transform.RotateAround(rightPivot.position, transform.up, steering * Time.deltaTime);
        }
    }
}
