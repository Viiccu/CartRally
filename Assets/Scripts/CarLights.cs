using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Car lights: a key turns the headlights/taillights on and off (every Light under this object, plus
/// the glow on the lamp meshes). Rear lamps glow brighter while braking and white while reversing,
/// even with the lights switched off, like a real car. Put it on the car's root.
/// </summary>
public class CarLights : MonoBehaviour
{
    public Key toggleKey = Key.L;
    public bool startOn = true;
    [Tooltip("Lamp meshes that glow. Leave empty to use every child whose name contains \"LAMPS\". Names containing \"REAR\" count as rear lamps.")]
    public Renderer[] lampRenderers;
    [Tooltip("How many times brighter the rear lamps glow while braking.")]
    public float brakeBrightness = 4f;
    [Tooltip("Glow of the rear lamps while reversing.")]
    [ColorUsage(false, true)] public Color reverseColor = Color.white * 2f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    Drivable car;
    Light[] lights;
    Material[] lampMaterials;
    Color[] glowColors;
    bool[] isRear;
    bool on;

    void Start()
    {
        car = GetComponentInParent<Drivable>();
        lights = GetComponentsInChildren<Light>(true);
        if (lampRenderers == null || lampRenderers.Length == 0)
            lampRenderers = System.Array.FindAll(GetComponentsInChildren<Renderer>(true), r => r.name.ToUpper().Contains("LAMPS"));

        int n = lampRenderers.Length;
        lampMaterials = new Material[n];
        glowColors = new Color[n];
        isRear = new bool[n];
        for (int i = 0; i < n; i++)
        {
            lampMaterials[i] = lampRenderers[i].material; // per-car copy, so other cars aren't affected
            glowColors[i] = lampMaterials[i].GetColor(EmissionColor);
            isRear[i] = lampRenderers[i].name.ToUpper().Contains("REAR");
        }
        on = startOn;
        foreach (var l in lights) l.enabled = on;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            on = !on;
            foreach (var l in lights) l.enabled = on;
        }

        bool reversing = car && car.Speed < 0f;
        bool braking = car && car.Braking;
        for (int i = 0; i < lampMaterials.Length; i++)
        {
            Color c = on ? glowColors[i] : Color.black;
            if (isRear[i] && reversing) c = reverseColor;
            else if (isRear[i] && braking) c = glowColors[i] * brakeBrightness;
            lampMaterials[i].SetColor(EmissionColor, c);
        }
    }
}
