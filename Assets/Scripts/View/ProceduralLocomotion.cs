using System.Collections.Generic;
using UnityEngine;

public class ProceduralLocomotion : MonoBehaviour
{
    [Header("Posture")]
    public float posturePitch = 0f;
    [Range(0f, 1f)] public float spineStiffness = 0f;
    
    private SimCreatureState _simState;

    private class SpineSegment
    {
        public Transform transform;
        public float length;
    }

    private class TailSegment
    {
        public Transform transform;
        public Quaternion baseLocalRotation;
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
        public Vector3 stepOffsetFromSocket;
        public Vector3 stepStart;
        public Vector3 stepEnd;
        public float stepProgress = 1f;
        public bool isStepping => stepProgress < 1f;
        public int gaitGroup;
    }

    private List<SpineSegment> _spine = new List<SpineSegment>();
    private List<TailSegment> _tail = new List<TailSegment>();
    private List<LimbIK> _limbs = new List<LimbIK>();
    private List<Vector3> _pathPositions = new List<Vector3>();
    private List<Quaternion> _pathRotations = new List<Quaternion>();
    private float _bodyHeightOffset;

    public void InitializeLocomotion(SimCreatureState state)
    {
        _simState = state;
        _spine.Clear();
        _tail.Clear();
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

        if (_spine.Count > 0)
        {
            Transform lastSpineTransform = _spine[_spine.Count - 1].transform;
            Transform currentTail = null;
            foreach (Transform child in lastSpineTransform)
            {
                if (child.name.StartsWith("Tail_1"))
                {
                    currentTail = child;
                    break;
                }
            }
            while (currentTail != null)
            {
                _tail.Add(new TailSegment { transform = currentTail, baseLocalRotation = currentTail.localRotation });
                Transform nextTail = null;
                foreach (Transform child in currentTail)
                {
                    if (child.name.StartsWith("Tail_"))
                    {
                        nextTail = child;
                        break;
                    }
                }
                currentTail = nextTail;
            }
        }

        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t.name.EndsWith("_Tip"))
            {
                BoneTag tipTag = t.GetComponent<BoneTag>();
                if (tipTag == null || tipTag.bone.Type == LimbType.Tail) continue;

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
                    
                    limb.attachedSpine = limb.joints[0].parent;
                    Vector3 restRoot = limb.joints[0].position;
                    Vector3 restTip = limb.joints[limb.joints.Length - 1].position;
                    Vector3 restDir = (restTip - restRoot).normalized;
                    Vector3 idealRest = restRoot + restDir * (limb.totalLength * 0.75f);
                    
                    if (limb.type == LimbType.Leg)
                    {
                        if (Physics.Raycast(idealRest + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                            idealRest = hit.point;
                        else
                            idealRest.y = 0f;
                            
                        Vector3 socketXZ = new Vector3(restRoot.x, 0, restRoot.z);
                        Vector3 tipXZ = new Vector3(idealRest.x, 0, idealRest.z);
                        limb.stepOffsetFromSocket = transform.InverseTransformDirection(tipXZ - socketXZ);
                    }
                    else
                    {
                        limb.stepOffsetFromSocket = Vector3.zero;
                    }

                    limb.ikTarget = idealRest;
                    limb.stepStart = idealRest;
                    limb.stepEnd = idealRest;
                    limb.stepProgress = 1f;
                    limb.phaseOffset = Random.Range(0f, Mathf.PI * 2f);
                    limb.restingPositionLocal = limb.attachedSpine.InverseTransformPoint(idealRest);
                    limb.restingDirLocal = limb.attachedSpine.InverseTransformDirection(restDir);

                    Vector3 limbDirLocal = limb.attachedSpine.InverseTransformDirection(restDir);
                    if (Mathf.Abs(limbDirLocal.x) > 0.5f) limb.localBendDir = Vector3.up;
                    else if (limbDirLocal.y < -0.5f) limb.localBendDir = Vector3.back;
                    else if (limbDirLocal.y > 0.5f) limb.localBendDir = Vector3.down;
                    else limb.localBendDir = Vector3.up;

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

        // --- Reverted exactly to original body height calculation ---
        float maxRequiredRootHeight = 0.5f;
        foreach (var limb in _limbs)
        {
            if (limb.type == LimbType.Leg)
            {
                int spineIdx = _spine.FindIndex(s => s.transform == limb.attachedSpine);
                float depth = 0f;
                for (int j = 1; j <= spineIdx; j++) depth += _spine[j - 1].length;
                
                Quaternion pitchedRot = Quaternion.Euler(-posturePitch, 0, 0);
                Vector3 localSpineOffset = pitchedRot * (Vector3.back * depth);
                float verticalDrop = localSpineOffset.y * spineStiffness;
                
                float stretchLimit = limb.totalLength * 0.75f;
                float horizontalSpread = limb.stepOffsetFromSocket.magnitude;
                float targetLegHeight = 0.5f;
                
                if (stretchLimit > horizontalSpread)
                    targetLegHeight = Mathf.Sqrt((stretchLimit * stretchLimit) - (horizontalSpread * horizontalSpread));
                    
                float requiredRootHeight = targetLegHeight - verticalDrop;
                if (requiredRootHeight > maxRequiredRootHeight)
                    maxRequiredRootHeight = requiredRootHeight;
            }
        }

        _bodyHeightOffset = maxRequiredRootHeight - 0.2f;
        if (_bodyHeightOffset < 0.5f) _bodyHeightOffset = 0.5f;

        for (int i = 0; i < 300; i++)
        {
            _pathPositions.Add(transform.position - transform.forward * (i * 0.1f));
            _pathRotations.Add(transform.rotation);
        }
    }

    void LateUpdate()
    {
        if (_spine.Count == 0 || _simState == null) return;

        Vector3 groundNormal = Vector3.up;
        float groundY = 0f;
        if (Physics.Raycast(_simState.Position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
        {
            groundNormal = hit.normal;
            groundY = hit.point.y;
        }

        Vector3 targetPos = _simState.Position;
        targetPos.y = Mathf.Lerp(transform.position.y, groundY + _bodyHeightOffset, Time.deltaTime * 5f);
        transform.position = targetPos;
        
        Quaternion baseRot = Quaternion.FromToRotation(Vector3.up, groundNormal) * _simState.Rotation;
        Quaternion targetRot = baseRot * Quaternion.Euler(-posturePitch, 0, 0);
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
            if (spineStiffness > 0.5f) wiggleAmplitude *= (1f - spineStiffness);
        }

        for (int i = 1; i < _spine.Count; i++)
        {
            totalDist += _spine[i - 1].length;
            float d = 0f;
            Vector3 pathTargetPos = _spine[i].transform.position;
            Quaternion pathTargetRot = _spine[i].transform.rotation;
            
            for (int p = 0; p < _pathPositions.Count - 1; p++)
            {
                float segDist = Vector3.Distance(_pathPositions[p], _pathPositions[p + 1]);
                if (d + segDist >= totalDist)
                {
                    float t = (totalDist - d) / segDist;
                    pathTargetPos = Vector3.Lerp(_pathPositions[p], _pathPositions[p + 1], t);
                    pathTargetRot = Quaternion.Slerp(_pathRotations[p], _pathRotations[p + 1], t);
                    break;
                }
                d += segDist;
            }

            Vector3 rigidTargetPos = _spine[i - 1].transform.position - _spine[i - 1].transform.forward * _spine[i - 1].length;
            Quaternion rigidTargetRot = _spine[i - 1].transform.rotation;
            Vector3 spineTargetPos = Vector3.Lerp(pathTargetPos, rigidTargetPos, spineStiffness);
            Quaternion spineTargetRot = Quaternion.Slerp(pathTargetRot, rigidTargetRot, spineStiffness);
            
            float wiggleAmount = Mathf.Sin(Time.time * 6f - i * 0.8f) * wiggleAmplitude;
            spineTargetPos += spineTargetRot * Vector3.right * wiggleAmount;
            _spine[i].transform.position = spineTargetPos;

            Vector3 dirToPrev = (_spine[i - 1].transform.position - _spine[i].transform.position).normalized;
            if (dirToPrev != Vector3.zero)
                _spine[i].transform.rotation = Quaternion.LookRotation(dirToPrev, spineTargetRot * Vector3.up);
        }

        for (int i = 0; i < _tail.Count; i++)
        {
            float tailWiggle = Mathf.Sin(Time.time * _simState.WalkSpeed * 2.5f - (i * 0.5f)) * 15f;
            _tail[i].transform.localRotation = _tail[i].baseLocalRotation * Quaternion.Euler(0, tailWiggle, 0);
        }

        // Stepping occurs faster now (taking only 40% of the movement duration)
        // This ensures one gait group easily finishes before the other is forced to move.
        float expectedStepTime = (_simState.StepDistance / Mathf.Max(_simState.WalkSpeed, 0.1f)) * 0.4f;
        float dynamicStepSpeed = 1f / Mathf.Clamp(expectedStepTime, 0.1f, 0.8f);

        for (int i = 0; i < _limbs.Count; i++)
        {
            LimbIK limb = _limbs[i];
            if (limb.type == LimbType.Leg)
            {
                Vector3 socketPos = limb.joints[0].position;
                Vector3 worldOffset = _simState.Rotation * limb.stepOffsetFromSocket;
                Vector3 desiredPos = socketPos + worldOffset;
                if (Physics.Raycast(desiredPos + Vector3.up * 5f, Vector3.down, out RaycastHit hitLeg, 10f))
                    desiredPos = hitLeg.point;
                else
                    desiredPos.y = groundY;

                float err = Vector2.Distance(new Vector2(limb.ikTarget.x, limb.ikTarget.z), new Vector2(desiredPos.x, desiredPos.z));
                float distToTarget = Vector3.Distance(socketPos, limb.ikTarget);
                
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
                
                // Emergency override if dragged far behind OR physically overstretched to avoid floating leg
                if (err > _simState.StepDistance * 1.25f || distToTarget > limb.totalLength * 0.98f) 
                    canStep = true;

                if (!limb.isStepping && err > _simState.StepDistance * 0.8f && canStep)
                {
                    limb.stepStart = limb.ikTarget;
                    
                    // Predict forward to ensure fast creatures don't instantly overtake their own feet again
                    Vector3 stepForward = transform.forward * (_simState.WalkSpeed * expectedStepTime * 1.2f);
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
                    currentPos.y += 4f * _simState.StepHeight * p * (1f - p);
                    limb.ikTarget = currentPos;
                }
                
                // Absolutely no code here dragging/sliding the IK target around when the leg isn't stepping.
                // The foot stays perfectly planted in world space.
            }
            else if (limb.type == LimbType.Manipulator)
            {
                Vector3 desiredPos = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
                desiredPos += limb.attachedSpine.forward * Mathf.Sin(Time.time * _simState.WalkSpeed * 2f + limb.phaseOffset) * 0.3f;
                limb.ikTarget = desiredPos;
            }
            else if (limb.type == LimbType.Tentacle)
            {
                Vector3 desiredPos = limb.attachedSpine.TransformPoint(limb.restingPositionLocal);
                desiredPos += limb.attachedSpine.right * Mathf.Sin(Time.time * 4f + limb.phaseOffset) * (limb.totalLength * 0.4f);
                desiredPos += limb.attachedSpine.up * Mathf.Cos(Time.time * 5f + limb.phaseOffset) * (limb.totalLength * 0.3f);
                limb.ikTarget = desiredPos;
            }
            else if (limb.type == LimbType.Horn)
            {
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
