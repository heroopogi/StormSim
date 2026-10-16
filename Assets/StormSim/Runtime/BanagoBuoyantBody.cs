using UnityEngine;

/// <summary>Distributed buoyancy and drag applied as forces, with optional slack mooring.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class BanagoBuoyantBody : MonoBehaviour
{
    public BanagoSeaWaves sea;
    [Tooltip("Local points below the waterline. Spread them across the hull and outriggers.")]
    public Vector3[] buoyancyPoints = new Vector3[0];
    [Tooltip("Probe immersion, in world metres, that supports the object's weight in calm water.")]
    [Min(.02f)] public float equilibriumDepth = .22f;
    [Min(0)] public float verticalDamping = 5;
    [Min(0)] public float waterResistance = 1.4f;
    public Vector3 localCenterOfMass = new Vector3(0, -.12f, 0);

    [Header("Mooring")]
    public bool moored = true;
    [Tooltip("Free horizontal travel before the mooring pulls back, in metres.")]
    [Min(0)] public float mooringRadius = 1.5f;
    [Min(0)] public float mooringStrength = .8f;
    [Tooltip("Mooring location in sea-local coordinates. Follows the coastline's parent transform.")]
    public Vector3 mooringAnchor;
    Rigidbody body;

    void Awake() => InitializeBody();

    public void InitializeBody()
    {
        body = GetComponent<Rigidbody>();
        // A low centre of mass represents the hull; the tall visual mast has negligible mass.
        body.centerOfMass = localCenterOfMass;
        body.maxAngularVelocity = 3;
        body.sleepThreshold = 0;
    }

    void FixedUpdate()
    {
        if (sea) ApplyWaterForces(sea.WaveTime);
    }

    // Public so the editor validation can run identical forces in an isolated PhysicsScene.
    public void ApplyWaterForces(float waveTime)
    {
        if (!body) InitializeBody();
        if (!sea || !sea.isActiveAndEnabled || body.isKinematic || buoyancyPoints.Length == 0) return;
        float pointMass = body.mass / buoyancyPoints.Length;
        float gravity = Mathf.Abs(Physics.gravity.y);
        foreach (Vector3 localPoint in buoyancyPoints)
        {
            Vector3 point = transform.TransformPoint(localPoint);
            if (!sea.TrySample(point, waveTime, out var water)) continue;
            float depth = water.height - point.y;
            if (depth <= 0) continue;
            float immersion = Mathf.Clamp01(depth / Mathf.Max(.02f, equilibriumDepth));
            Vector3 relativeVelocity = body.GetPointVelocity(point) - water.velocity;
            float lift = gravity * Mathf.Clamp(depth / Mathf.Max(.02f, equilibriumDepth), 0, 3)
                - relativeVelocity.y * verticalDamping * immersion;
            // Buoyancy never pulls a dry hull down; gravity supplies its downward acceleration.
            Vector3 force = Vector3.up * (pointMass * Mathf.Clamp(lift, 0, gravity * 4));
            force -= new Vector3(relativeVelocity.x, 0, relativeVelocity.z) * (pointMass * waterResistance * immersion);
            body.AddForceAtPosition(force, point, ForceMode.Force);
        }
        if (!moored) return;
        Vector3 offset = body.worldCenterOfMass - sea.transform.TransformPoint(mooringAnchor);
        offset.y = 0;
        float distance = offset.magnitude;
        if (distance <= mooringRadius || distance < .0001f) return;
        Vector3 horizontalVelocity = new Vector3(body.linearVelocity.x, 0, body.linearVelocity.z);
        body.AddForce((-offset.normalized * ((distance - mooringRadius) * mooringStrength)
            - horizontalVelocity * .7f) * body.mass, ForceMode.Force);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var point in buoyancyPoints) Gizmos.DrawSphere(transform.TransformPoint(point), .06f);
        if (sea && moored)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, sea.transform.TransformPoint(mooringAnchor));
        }
    }
}
