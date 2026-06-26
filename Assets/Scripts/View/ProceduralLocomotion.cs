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

    // Simulation Data Reference
    private SimCreatureState _simState;

    private class SpineSegment
    {
        public Transform transform;
        public float length;
    }
    
    private class LimbIK
    {
        public LimbType type;
        public Transform[] joints;
        public float[] lengths;
        public float totalLength;
        public Vector3 ikTarget;
        public Transform attachedSpine;
        
        public Vector3 restingPositionLocal;
        public Vector3 localBendDir;
        public Vector3 restingDirLocal;
        public float phaseOffset;

        // Leg specific variables
        public Vector3 stepStart;
        public Vector3 stepEnd;
        public float stepProgress = 1f;
        public bool isStepping => stepProgress < 1f;
        public int gaitGroup;
    }
    
    private List<SpineSegment> _spine = new List<SpineSegment>();
    private List<LimbIK> _limbs = new List<LimbIK>();
    private List<Vector3> _pathPositions = new List<Vector3>();
    private List<Quaternion> _pathRotations = new List<Quaternion>();
    
    private float _bodyHeightOffset;
    private float _heading;
    
    public void InitializeLocomotion()
    {
        _simState = new SimCreatureState
        {
            Position = transform.position,
            Velocity = Vector3.zero,
            Mass = 50f
        };

        _spine.Clear();
        _limbs.Clear();
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
                BoneTag tipTag = t.GetComponent<BoneTag>();
                if (tipTag == null) continue;

                List<Transform> chain = new List<Transform>();
                Transform curr = t;
                
                while (curr != null && curr != transform && !curr.name.Contains("Spine_"))
                {
                    if (curr.name != "InfoLabel") chain.Add(curr);
                    BoneTag bt = curr.GetComponent<BoneTag>();
                    if (bt != null && bt.bone.Name.Contains("_J0")) break;
                    curr = curr.parent;
                }
                
                if (chain.Count > 0)
                {
                    chain.Reverse();
                    LimbIK limb = new LimbIK();
                    limb.type = tipTag.bone.Type;
                    limb.joints = chain.ToArray();
                    limb.lengths = new float[limb.joints.Length - 1];
                    limb.totalLength = 0f;
                    
                    for (int i = 0; i < limb.joints.Length - 1; i++)
                    {
                        float dist = Vector3.Distance(limb.joints[i].position, limb.joints[i+1].position);
                        if (dist <= 0.01f) dist = 0.1f;
                        limb.lengths[i] = dist;
                        limb.totalLength += dist;
                    }
                    
                    if (limb.type == LimbType.Leg && limb.totalLength > maxLegLength) maxLegLength = limb.totalLength;
                    
                    limb.attachedSpine = limb.joints[0].parent;
                    Vector3 restRoot = limb.joints[0].position;
                    Vector3 restTip = limb.joints[limb.joints.Length - 1].position;
                    Vector3 restDir = (restTip - restRoot).normalized;
                    Vector3 idealRest = restRoot + restDir * (limb.totalLength * 0.75f);
                    
                    // FIX: Only force legs to touch the floor! Let arms, tentacles, and horns hang in the air.
                    if (limb.type == LimbType.Leg)
                    {
                        if (Physics.Raycast(idealRest + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                            idealRest = hit.point;
                        else
                            idealRest.y = 0f;
                    }
                        
                    limb.ikTarget = idealRest;
                    limb.stepStart = idealRest;
                    limb.stepEnd = idealRest;
                    limb.stepProgress = 1f;
                    limb.phaseOffset = Random.Range(0f, Mathf.PI * 2f);
                    limb.restingPositionLocal = limb.attachedSpine.InverseTransformPoint(idealRest);
                    limb.restingDirLocal = limb.attachedSpine.InverseTransformDirection(restDir);
                    
                    Vector3 limbDirLocal = limb.attachedSpine.InverseTransformDirection(restDir);
                    if (limbDirLocal.y < -0.5f) {
                        limb.localBendDir = Vector3.forward;
                    } else if (limbDirLocal.y > 0.5f) {
                        limb.localBendDir = Vector3.down;
                    } else {
                        limb.localBendDir = Vector3.up;
                    }
                    _limbs.Add(limb);
                }
            }
        }
        
        int legIndex = 0;
        for (int i = 0; i < _limbs.Count; i++)
        {
            if (_limbs[i].type == LimbType.Leg)
            {
                _limbs[i].gaitGroup = (legIndex + (legIndex / 2)) % 2;
                legIndex++;
            }
        }
        
        _heading = transform.eulerAngles.y;
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
        if (_spine.Count == 0 || _simState == null) return;
        
        // 1. HEADLESS SIMULATION UPDATE
        _heading += turnSpeed * Time.deltaTime;
        Quaternion headingRot = Quaternion.Euler(0, _heading, 0);
        _simState.Velocity = headingRot * Vector3.forward * walkSpeed;
        
        Vector3 nextPos = _simState.Position + _simState.Velocity * Time.deltaTime;
        Vector3 groundNormal = Vector3.up;
        float groundY = 0f;
        
        if (Physics.Raycast(nextPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
        {
            groundNormal = hit.normal;
            groundY = hit.point.y;
        }
        
        Vector3 targetPos = nextPos;
        targetPos.y = Mathf.Lerp(_simState.Position.y, groundY + _bodyHeightOffset, Time.deltaTime * 5f);
        _simState.Position = targetPos; // State mathematically advanced


        // 2. VIEW LAYER UPDATE (Presentation strictly driven by state)
        transform.position = _simState.Position;
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
                _spine[i].transform.rotation = Quaternion.LookRotation(dirToPrev, spineTargetRot * Vector3.up);
        }
        
        for (int i = 0; i < _limbs.Count; i++)
        {
            LimbIK limb = _limbs[i];
            
            if (limb.type == LimbType.Leg)
            {
                Vector3 desiredPos = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
                if (Physics.Raycast(desiredPos + Vector3.up * 5f, Vector3.down, out RaycastHit hitLeg, 10f))
                    desiredPos = hitLeg.point;
                else
                    desiredPos.y = groundY;
                    
                float err = Vector2.Distance(new Vector2(limb.ikTarget.x, limb.ikTarget.z), new Vector2(desiredPos.x, desiredPos.z));
                bool opposingGroupStepping = false;
                
                foreach (var l in _limbs)
                {
                    if (l.type == LimbType.Leg && l.gaitGroup != limb.gaitGroup && l.isStepping)
                    {
                        opposingGroupStepping = true;
                        break;
                    }
                }
                
                bool canStep = !opposingGroupStepping;
                if (err > stepDistance * 1.5f) canStep = true;
                
                float dynamicStepSpeed = Mathf.Max(stepSpeed, walkSpeed * 2.5f);
                if (!limb.isStepping && err > stepDistance && canStep)
                {
                    limb.stepStart = limb.ikTarget;
                    float stepDuration = 1f / dynamicStepSpeed;
                    Vector3 stepForward = transform.forward * (walkSpeed * stepDuration * 1.5f);
                    limb.stepEnd = desiredPos + stepForward;
                    if (Physics.Raycast(limb.stepEnd + Vector3.up * 5f, Vector3.down, out RaycastHit hitEnd, 10f))
                        limb.stepEnd = hitEnd.point;
                    else
                        limb.stepEnd.y = groundY;
                    limb.stepProgress = 0f;
                }
                
                if (limb.isStepping)
                {
                    limb.stepProgress += Time.deltaTime * dynamicStepSpeed;
                    if (limb.stepProgress >= 1f) limb.stepProgress = 1f;
                    Vector3 currentPos = Vector3.Lerp(limb.stepStart, limb.stepEnd, limb.stepProgress);
                    float p = limb.stepProgress;
                    currentPos.y += 4f * stepHeight * p * (1f - p);
                    limb.ikTarget = currentPos;
                }
                
                if (!limb.isStepping)
                {
                    Vector3 rootPos = limb.joints[0].position;
                    Vector3 restDirWorld = limb.attachedSpine.TransformDirection(limb.restingDirLocal);
                    Vector3 currentDir = (limb.ikTarget - rootPos).normalized;
                    float angle = Vector3.Angle(restDirWorld, currentDir);
                    float maxAngle = 75f;
                    if (angle > maxAngle)
                    {
                        Vector3 clampedDir = Vector3.Slerp(restDirWorld, currentDir, maxAngle / angle);
                        float dist = Vector3.Distance(rootPos, limb.ikTarget);
                        Vector3 newTarget = rootPos + clampedDir * dist;
                        newTarget.y = limb.ikTarget.y;
                        limb.ikTarget = newTarget;
                    }
                }
            }
            else if (limb.type == LimbType.Manipulator)
            {
                // Manipulators lightly swing with the movement
                Vector3 desiredPos = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
                desiredPos += limb.attachedSpine.forward * Mathf.Sin(Time.time * walkSpeed * 2f + limb.phaseOffset) * 0.3f;
                limb.ikTarget = desiredPos;
            }
            else if (limb.type == LimbType.Tentacle)
            {
                // Tentacles procedurally wiggle out and curl back using sine waves
                Vector3 desiredPos = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
                desiredPos += limb.attachedSpine.right * Mathf.Sin(Time.time * 4f + limb.phaseOffset) * (limb.totalLength * 0.4f);
                desiredPos += limb.attachedSpine.up * Mathf.Cos(Time.time * 5f + limb.phaseOffset) * (limb.totalLength * 0.3f);
                limb.ikTarget = desiredPos;
            }
            else if (limb.type == LimbType.Horn)
            {
                // Horns remain rigid and track their assigned resting position perfectly
                limb.ikTarget = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
            }
            
            ApplyFABRIK(limb);
        }
    }
    
    private void ApplyFABRIK(LimbIK limb)
    {
        int numJoints = limb.joints.Length;
        Vector3[] positions = new Vector3[numJoints];
        for (int i = 0; i < numJoints; i++) positions[i] = limb.joints[i].position;
        
        Vector3 rootPos = positions[0];
        Vector3 targetPos = limb.ikTarget;
        
        if (Vector3.Distance(rootPos, targetPos) < 0.01f) return;
        
        Vector3 worldBendDir = limb.attachedSpine.TransformDirection(limb.localBendDir);
        Vector3 rootToTarget = (targetPos - rootPos).normalized;
        Vector3 planeNormal = Vector3.Cross(rootToTarget, worldBendDir).normalized;
        if (planeNormal.sqrMagnitude < 0.01f) planeNormal = limb.attachedSpine.right;
        
        if (Vector3.Distance(rootPos, targetPos) > limb.totalLength * 0.99f)
        {
            Vector3 dir = rootToTarget;
            for (int i = 1; i < numJoints; i++)
                positions[i] = positions[i - 1] + dir * limb.lengths[i - 1];
        }
        else
        {
            for (int i = 1; i < numJoints - 1; i++) 
            {
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
                    positions[i] = positions[i + 1] + dir * limb.lengths[i];
                }
                
                positions[0] = rootPos;
                for (int i = 1; i < numJoints; i++)
                {
                    Vector3 dir = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + dir * limb.lengths[i - 1];
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
                limb.joints[i].rotation = Quaternion.LookRotation(dir, boneUp);
            }
            limb.joints[i].position = positions[i];
        }
        limb.joints[numJoints - 1].position = positions[numJoints - 1];
    }
}
