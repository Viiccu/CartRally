using UnityEngine;
using UnityEngine.InputSystem;


public class Drivable : MonoBehaviour
{
    public float maxSpeed = 20f;
    public float steering = 90f;
    public float acceleration = 1f;
    public Transform pivot;

    private float speed = 0;

    void Start(){

    }

    void Update(){
        if(Keyboard.current == null) return;

        if(Keyboard.current.wKey.isPressed){
            speed = speed < maxSpeed ? speed + acceleration : speed; 
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        } else if(speed > 0){
            speed -= acceleration * 1/2;
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }

        if(speed > 0 && Keyboard.current.aKey.isPressed){
            transform.RotateAround(pivot.position, transform.up, -steering * Time.deltaTime);
        }

        if(speed > 0 && Keyboard.current.dKey.isPressed){
            transform.RotateAround(pivot.position, transform.up, steering * Time.deltaTime);
        }
    }
}
