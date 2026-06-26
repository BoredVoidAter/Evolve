using System.Collections.Generic;
using UnityEngine;

public class ProceduralLocomotion : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float turnSpeed = 25.0f;

    [Header("Stepping")]
    public float stepDistance = 0.5f;
    public float stepHeight = 0.4f;
    public float stepSpeed = 5.0f;

    private class SpineSegment
    {
        public Transform transform;
        public float length;
    }

    private class Leg
    {
        public Transform[] joints;
        public float[] lengths;
        public float totalLength;
        public Vector3 ikTarget;
        public Vector3 stepStart;
        public Vector3 stepEnd;
        public float stepProgress = 1f;
        public Vector3 restingPositionLocal;
        public bool isStepping => stepProgress < 1f;
        public Transform attachedSpine;
        public Vector3 localBendDir;
        public int gaitGroup;
        
        public Vector3 restingDirLocal;
    }

    private List<SpineSegment> _spine = new List<SpineSegment>();
    private List<Leg> _legs = new List<Leg>();
    private List<Vector3> _pathPositions = new List<Vector3>();
    private List<Quaternion> _pathRotations = new List<Quaternion>();
    private float _bodyHeightOffset;
    private float _heading;

    public void InitializeLocomotion()
    {
        _spine.Clear();
        _legs.Clear();
        _pathPositions.Clear();
        _pathRotations.Clear();

        Transform currentSpine = transform.Find("Spine_0 (Root)");
        while (currentSpine != null)
        {
            SpineSegment seg = new SpineSegment { transform = currentSpine, length = 0f };
            Transform nextSpine = null;

            foreach (Transform child in currentSpine)
            {
                if (child.name.StartsWith("Spine_"))
                {
                    nextSpine = child;
                    seg.length = child.localPosition.z;
                    break;
                }
            }

            if (nextSpine == null && currentSpine.childCount > 0)
                seg.length = 0.5f;

            _spine.Add(seg);
            currentSpine = nextSpine;
        }

        float maxLegLength = 0f;
        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        
        foreach (Transform t in allTransforms)
        {
            if (t.name.EndsWith("_Tip"))
            {
                List<Transform> chain = new List<Transform>();
                Transform curr = t;

                while (curr != null && curr != transform && !curr.name.Contains("Spine_"))
                {
                    if (curr.name != "InfoLabel") chain.Add(curr);
                    if (curr.name.Contains("_J0")) break;
                    curr = curr.parent;
                }

                if (chain.Count > 0 && chain[chain.Count - 1].name.Contains("_J0"))
                {
                    chain.Reverse();
                    Leg leg = new Leg();
                    leg.joints = chain.ToArray();
                    leg.lengths = new float[leg.joints.Length - 1];
                    leg.totalLength = 0f;

                    for (int i = 0; i < leg.joints.Length - 1; i++)
                    {
                        float dist = Vector3.Distance(leg.joints[i].position, leg.joints[i+1].position);
                        if (dist <= 0.01f) dist = 0.1f;
                        leg.lengths[i] = dist;
                        leg.totalLength += dist;
                    }

                    if (leg.totalLength > maxLegLength) maxLegLength = leg.totalLength;

                    leg.attachedSpine = leg.joints[0].parent;

                    Vector3 restRoot = leg.joints[0].position;
                    Vector3 restTip = leg.joints[leg.joints.Length - 1].position;
                    Vector3 restDir = (restTip - restRoot).normalized;
                    
                    // Reduced from 0.8f to 0.75f to encourage resting slightly closer to the body (more bend)
                    Vector3 idealRest = restRoot + restDir * (leg.totalLength * 0.75f);

                    if (Physics.Raycast(idealRest + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                        idealRest = hit.point;
                    else
                        idealRest.y = 0f;

                    leg.ikTarget = idealRest;
                    leg.stepStart = idealRest;
                    leg.stepEnd = idealRest;
                    leg.stepProgress = 1f;
                    leg.restingPositionLocal = leg.attachedSpine.InverseTransformPoint(idealRest);
                    
                    leg.restingDirLocal = leg.attachedSpine.InverseTransformDirection(restDir);

                    Vector3 limbDirLocal = leg.attachedSpine.InverseTransformDirection(restDir);
                    if (limbDirLocal.y < -0.5f) {
                        leg.localBendDir = Vector3.forward;
                    } else if (limbDirLocal.y > 0.5f) {
                        leg.localBendDir = Vector3.down;
                    } else {
                        leg.localBendDir = Vector3.up;
                    }

                    _legs.Add(leg);
                }
            }
        }

        for (int i = 0; i < _legs.Count; i++)
        {
            _legs[i].gaitGroup = (i + (i / 2)) % 2;
        }

        _heading = transform.eulerAngles.y;

        // Force the body to hover at 65% of the max leg length to guarantee bent knees/joints
        _bodyHeightOffset = maxLegLength * 0.65f;
        if (_bodyHeightOffset < 0.5f) _bodyHeightOffset = 0.5f;

        for (int i = 0; i < 300; i++)
        {
            _pathPositions.Add(transform.position - transform.forward * (i * 0.1f));
            _pathRotations.Add(transform.rotation);
        }
    }

    void Update()
    {
        if (_spine.Count == 0) return;

        _heading += turnSpeed * Time.deltaTime;
        Quaternion headingRot = Quaternion.Euler(0, _heading, 0);

        Vector3 velocity = headingRot * Vector3.forward * walkSpeed;
        Vector3 nextPos = transform.position + velocity * Time.deltaTime;

        Vector3 groundNormal = Vector3.up;
        float groundY = 0f;
        if (Physics.Raycast(nextPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
        {
            groundNormal = hit.normal;
            groundY = hit.point.y;
        }

        Vector3 targetPos = nextPos;
        targetPos.y = Mathf.Lerp(transform.position.y, groundY + _bodyHeightOffset, Time.deltaTime * 5f);
        transform.position = targetPos;

        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, groundNormal) * headingRot;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);

        float distSinceLast = Vector3.Distance(transform.position, _pathPositions[0]);
        if (distSinceLast > 0.05f)
        {
            _pathPositions.Insert(0, transform.position);
            _pathRotations.Insert(0, transform.rotation);

            if (_pathPositions.Count > 300)
            {
                _pathPositions.RemoveAt(_pathPositions.Count - 1);
                _pathRotations.RemoveAt(_pathRotations.Count - 1);
            }
        }

        _spine[0].transform.position = transform.position;
        _spine[0].transform.rotation = transform.rotation;

        float totalDist = 0f;
        float wiggleAmplitude = 0f;
        
        if (_spine.Count > 1)
        {
            float spineFactor = Mathf.Clamp01((_spine.Count - 2) / 8f);
            wiggleAmplitude = Mathf.Lerp(0.0f, 0.15f, spineFactor);
        }

        for (int i = 1; i < _spine.Count; i++)
        {
            totalDist += _spine[i - 1].length;
            float d = 0f;

            Vector3 spineTargetPos = _spine[i].transform.position;
            Quaternion spineTargetRot = _spine[i].transform.rotation;

            for (int p = 0; p < _pathPositions.Count - 1; p++)
            {
                float segDist = Vector3.Distance(_pathPositions[p], _pathPositions[p + 1]);
                if (d + segDist >= totalDist)
                {
                    float t = (totalDist - d) / segDist;
                    spineTargetPos = Vector3.Lerp(_pathPositions[p], _pathPositions[p + 1], t);
                    spineTargetRot = Quaternion.Slerp(_pathRotations[p], _pathRotations[p + 1], t);
                    break;
                }
                d += segDist;
            }

            float wiggleAmount = Mathf.Sin(Time.time * 6f - i * 0.8f) * wiggleAmplitude;
            spineTargetPos += spineTargetRot * Vector3.right * wiggleAmount;

            _spine[i].transform.position = spineTargetPos;

            Vector3 dirToPrev = (_spine[i - 1].transform.position - _spine[i].transform.position).normalized;
            if (dirToPrev != Vector3.zero)
            {
                _spine[i].transform.rotation = Quaternion.LookRotation(dirToPrev, spineTargetRot * Vector3.up);
            }
        }

        for (int i = 0; i < _legs.Count; i++)
        {
            Leg leg = _legs[i];

            Vector3 desiredPos = leg.attachedSpine.TransformPoint(leg.restingPositionLocal);
            if (Physics.Raycast(desiredPos + Vector3.up * 5f, Vector3.down, out RaycastHit hitLeg, 10f))
                desiredPos = hitLeg.point;
            else
                desiredPos.y = groundY;

            float err = Vector2.Distance(new Vector2(leg.ikTarget.x, leg.ikTarget.z), new Vector2(desiredPos.x, desiredPos.z));

            bool opposingGroupStepping = false;
            foreach (var l in _legs)
            {
                if (l.gaitGroup != leg.gaitGroup && l.isStepping)
                {
                    opposingGroupStepping = true;
                    break;
                }
            }

            bool canStep = !opposingGroupStepping;
            if (err > stepDistance * 1.5f) canStep = true;

            // Dynamically scale step speed based on walk speed to prevent dragging
            float dynamicStepSpeed = Mathf.Max(stepSpeed, walkSpeed * 2.5f);

            if (!leg.isStepping && err > stepDistance && canStep)
            {
                leg.stepStart = leg.ikTarget;

                // Predict future step end position based on body velocity
                float stepDuration = 1f / dynamicStepSpeed;
                Vector3 stepForward = transform.forward * (walkSpeed * stepDuration * 1.5f);

                leg.stepEnd = desiredPos + stepForward;
                if (Physics.Raycast(leg.stepEnd + Vector3.up * 5f, Vector3.down, out RaycastHit hitEnd, 10f))
                    leg.stepEnd = hitEnd.point;
                else
                    leg.stepEnd.y = groundY;

                leg.stepProgress = 0f;
            }

            if (leg.isStepping)
            {
                leg.stepProgress += Time.deltaTime * dynamicStepSpeed;
                if (leg.stepProgress >= 1f) leg.stepProgress = 1f;

                Vector3 currentPos = Vector3.Lerp(leg.stepStart, leg.stepEnd, leg.stepProgress);
                float p = leg.stepProgress;
                currentPos.y += 4f * stepHeight * p * (1f - p);
                
                leg.ikTarget = currentPos;
            }

            // Only apply anti-entanglement clamp if the leg is NOT actively stepping
            // This allows the step to cleanly complete its motion without artificial walls cutting it short
            if (!leg.isStepping)
            {
                Vector3 rootPos = leg.joints[0].position;
                Vector3 restDirWorld = leg.attachedSpine.TransformDirection(leg.restingDirLocal);
                Vector3 currentDir = (leg.ikTarget - rootPos).normalized;
                float angle = Vector3.Angle(restDirWorld, currentDir);
                
                float maxAngle = 75f; // Generous angle allowance
                
                if (angle > maxAngle)
                {
                    Vector3 clampedDir = Vector3.Slerp(restDirWorld, currentDir, maxAngle / angle);
                    float dist = Vector3.Distance(rootPos, leg.ikTarget);
                    Vector3 newTarget = rootPos + clampedDir * dist;
                    newTarget.y = leg.ikTarget.y; 
                    leg.ikTarget = newTarget;
                }
            }

            ApplyFABRIK(leg);
        }
    }

    private void ApplyFABRIK(Leg leg)
    {
        int numJoints = leg.joints.Length;
        Vector3[] positions = new Vector3[numJoints];

        for (int i = 0; i < numJoints; i++) positions[i] = leg.joints[i].position;

        Vector3 rootPos = positions[0];
        Vector3 targetPos = leg.ikTarget;

        if (Vector3.Distance(rootPos, targetPos) < 0.01f) return;

        Vector3 worldBendDir = leg.attachedSpine.TransformDirection(leg.localBendDir);
        Vector3 rootToTarget = (targetPos - rootPos).normalized;
        Vector3 planeNormal = Vector3.Cross(rootToTarget, worldBendDir).normalized;
        if (planeNormal.sqrMagnitude < 0.01f) planeNormal = leg.attachedSpine.right;

        if (Vector3.Distance(rootPos, targetPos) > leg.totalLength * 0.99f)
        {
            Vector3 dir = rootToTarget;
            for (int i = 1; i < numJoints; i++)
                positions[i] = positions[i - 1] + dir * leg.lengths[i - 1];
        }
        else
        {
            for (int i = 1; i < numJoints - 1; i++) {
                Vector3 offset = positions[i] - rootPos;
                offset = Vector3.ProjectOnPlane(offset, planeNormal);
                offset += worldBendDir * 0.1f;
                positions[i] = rootPos + offset;
            }

            for (int iter = 0; iter < 10; iter++)
            {
                positions[numJoints - 1] = targetPos;
                for (int i = numJoints - 2; i >= 0; i--)
                {
                    Vector3 dir = (positions[i] - positions[i + 1]).normalized;
                    positions[i] = positions[i + 1] + dir * leg.lengths[i];
                }

                positions[0] = rootPos;
                for (int i = 1; i < numJoints; i++)
                {
                    Vector3 dir = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + dir * leg.lengths[i - 1];
                }

                for (int i = 1; i < numJoints - 1; i++)
                {
                    Vector3 offset = positions[i] - rootPos;
                    offset = Vector3.ProjectOnPlane(offset, planeNormal);
                    positions[i] = rootPos + offset;
                }

                if (Vector3.Distance(positions[numJoints - 1], targetPos) < 0.01f)
                    break;
            }
        }

        for (int i = 0; i < numJoints - 1; i++)
        {
            Vector3 dir = (positions[i + 1] - positions[i]).normalized;
            Vector3 boneUp = Vector3.Cross(dir, planeNormal).normalized;
            if (Vector3.Dot(boneUp, worldBendDir) < 0) boneUp = -boneUp;

            if (dir != Vector3.zero && boneUp != Vector3.zero)
            {
                leg.joints[i].rotation = Quaternion.LookRotation(dir, boneUp);
            }
            leg.joints[i].position = positions[i];
        }

        leg.joints[numJoints - 1].position = positions[numJoints - 1];
    }
}
