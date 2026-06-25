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
    }

    private List<SpineSegment> _spine = new List<SpineSegment>();
    private List<Leg> _legs = new List<Leg>();
    private List<Vector3> _pathPositions = new List<Vector3>();
    private List<Quaternion> _pathRotations = new List<Quaternion>();

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

        Transform[] allTransforms = GetComponentsInChildren<Transform>();
        foreach (Transform t in allTransforms)
        {
            // FIX: Instead of searching for childCount == 0 (which failed due to InfoLabels), 
            // we look explicitly for our new "_Tip" dummy transforms that act as real end-effectors.
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
                    
                    leg.attachedSpine = leg.joints[0].parent;
                    Vector3 endPos = leg.joints[leg.joints.Length - 1].position;
                    
                    if (Physics.Raycast(endPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                        leg.ikTarget = hit.point;
                    else
                        leg.ikTarget = new Vector3(endPos.x, 0f, endPos.z);
                        
                    leg.stepStart = leg.ikTarget;
                    leg.stepEnd = leg.ikTarget;
                    leg.stepProgress = 1f;
                    leg.restingPositionLocal = leg.attachedSpine.InverseTransformPoint(leg.ikTarget);
                    
                    _legs.Add(leg);
                }
            }
        }

        for (int i = 0; i < 50; i++)
        {
            _pathPositions.Add(transform.position - transform.forward * (i * 0.1f));
            _pathRotations.Add(transform.rotation);
        }
    }

    void Update()
    {
        if (_spine.Count == 0) return;

        transform.Translate(Vector3.forward * walkSpeed * Time.deltaTime);
        transform.Rotate(0, turnSpeed * Time.deltaTime, 0);

        float distSinceLast = Vector3.Distance(transform.position, _pathPositions[0]);
        if (distSinceLast > 0.05f)
        {
            _pathPositions.Insert(0, transform.position);
            _pathRotations.Insert(0, transform.rotation);
            if (_pathPositions.Count > 200)
            {
                _pathPositions.RemoveAt(_pathPositions.Count - 1);
                _pathRotations.RemoveAt(_pathRotations.Count - 1);
            }
        }

        float wiggle = Mathf.Sin(Time.time * 4f) * 12f;
        float totalDist = 0f;

        for (int i = 1; i < _spine.Count; i++)
        {
            totalDist += _spine[i - 1].length;
            float d = 0f;
            for (int p = 0; p < _pathPositions.Count - 1; p++)
            {
                float segDist = Vector3.Distance(_pathPositions[p], _pathPositions[p + 1]);
                if (d + segDist >= totalDist)
                {
                    float t = (totalDist - d) / segDist;
                    Vector3 targetPos = Vector3.Lerp(_pathPositions[p], _pathPositions[p + 1], t);
                    Quaternion targetRot = Quaternion.Slerp(_pathRotations[p], _pathRotations[p + 1], t);
                    targetRot *= Quaternion.Euler(0, wiggle * (i % 2 == 0 ? 1 : -1), 0);
                    _spine[i].transform.position = targetPos;
                    _spine[i].transform.rotation = targetRot;
                    break;
                }
                d += segDist;
            }
        }

        int steppingCount = 0;
        foreach (var leg in _legs) if (leg.isStepping) steppingCount++;
        int maxStepping = Mathf.Max(1, _legs.Count / 3);

        for (int i = 0; i < _legs.Count; i++)
        {
            Leg leg = _legs[i];
            Vector3 desiredPos = leg.attachedSpine.TransformPoint(leg.restingPositionLocal);
            
            if (Physics.Raycast(desiredPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 10f))
                desiredPos = hit.point;
            else
                desiredPos.y = 0f;
                
            float err = Vector3.Distance(leg.ikTarget, desiredPos);
            bool canStepNow = true;
            
            if (i > 0 && _legs[i-1].isStepping) canStepNow = false;
            if (i < _legs.Count - 1 && _legs[i+1].isStepping) canStepNow = false;
            
            if (!leg.isStepping && err > stepDistance && steppingCount < maxStepping && canStepNow)
            {
                leg.stepStart = leg.ikTarget;
                leg.stepEnd = desiredPos + (transform.forward * walkSpeed * 0.35f);
                leg.stepProgress = 0f;
                steppingCount++;
            }

            if (leg.isStepping)
            {
                leg.stepProgress += Time.deltaTime * stepSpeed;
                if (leg.stepProgress >= 1f) leg.stepProgress = 1f;
                
                Vector3 currentPos = Vector3.Lerp(leg.stepStart, leg.stepEnd, leg.stepProgress);
                currentPos.y += Mathf.Sin(leg.stepProgress * Mathf.PI) * stepHeight;
                leg.ikTarget = currentPos;
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

        if (Vector3.Distance(rootPos, targetPos) > leg.totalLength * 0.99f)
        {
            Vector3 dir = (targetPos - rootPos).normalized;
            for (int i = 1; i < numJoints; i++)
                positions[i] = positions[i - 1] + dir * leg.lengths[i - 1];
        }
        else
        {
            for (int iter = 0; iter < 10; iter++)
            {
                // Backward
                positions[numJoints - 1] = targetPos;
                for (int i = numJoints - 2; i >= 0; i--)
                {
                    Vector3 dir = (positions[i] - positions[i + 1]).normalized;
                    positions[i] = positions[i + 1] + dir * leg.lengths[i];
                }
                
                // Forward
                positions[0] = rootPos;
                for (int i = 1; i < numJoints; i++)
                {
                    Vector3 dir = (positions[i] - positions[i - 1]).normalized;
                    positions[i] = positions[i - 1] + dir * leg.lengths[i - 1];
                }
                
                if (Vector3.Distance(positions[numJoints - 1], targetPos) < 0.01f)
                    break;
            }
        }

        for (int i = 0; i < numJoints - 1; i++)
        {
            Vector3 dir = (positions[i + 1] - positions[i]).normalized;
            leg.joints[i].position = positions[i];
            
            Vector3 upHint = Vector3.up;
            if (leg.attachedSpine != null)
            {
                Vector3 awayFromBody = (positions[i] - leg.attachedSpine.position).normalized;
                awayFromBody.y = 0;
                upHint = (Vector3.up + awayFromBody * 0.5f).normalized;
            }
            leg.joints[i].rotation = Quaternion.LookRotation(dir, upHint);
        }
        leg.joints[numJoints - 1].position = positions[numJoints - 1];
    }
}
