using System.Collections.Generic;
using UnityEngine;

public class SimCreatureState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 Velocity;
    public float Mass;
    public Vector3 CenterOfMass;
    public float BoundingRadius;
    public SimBone RootBone;
    public float WalkSpeed;
    public float TurnSpeed;
    public float StepDistance;
    public float StepHeight;
    public float EnergyCost;
    public float Heading;

    public void UpdateSimulation(float dt)
    {
        Heading += TurnSpeed * dt;
        Rotation = Quaternion.Euler(0, Heading, 0);
        Velocity = Rotation * Vector3.forward * WalkSpeed;
        Position += Velocity * dt;
    }
}

public class SimBone
{
    public string Name;
    public LimbType Type;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public float Length;
    public SimBone Parent;
    public List<SimBone> Children = new List<SimBone>();
    public bool IsEndEffector => Children.Count == 0;
}

public static class SkeletonGenerator
{
    public static SimCreatureState GenerateCreatureState(AnimalDNA dna, Vector3 startPos)
    {
        SimBone root = GenerateSkeleton(dna.BodyPlan);
        SimCreatureState state = new SimCreatureState
        {
            RootBone = root,
            Position = startPos,
            Rotation = Quaternion.identity,
            Heading = 0f
        };
        CalculateBiometrics(state, dna.BodyPlan);
        return state;
    }

    private static void CalculateBiometrics(SimCreatureState state, BodyPlanDNA dna)
    {
        float totalBoneLength = CalculateTotalBoneLength(state.RootBone);
        state.Mass = totalBoneLength * 8f;
        
        List<Vector3> allPoints = new List<Vector3>();
        ComputeModelSpace(state.RootBone, Vector3.zero, Quaternion.identity, allPoints);
        Vector3 com = Vector3.zero;
        foreach (var pt in allPoints) com += pt;
        if (allPoints.Count > 0) com /= allPoints.Count;
        state.CenterOfMass = com;
        
        float maxDistSq = 0f;
        foreach (var pt in allPoints)
        {
            float distSq = (pt - com).sqrMagnitude;
            if (distSq > maxDistSq) maxDistSq = distSq;
        }
        state.BoundingRadius = Mathf.Sqrt(maxDistSq);
        
        List<SimBone> legs = new List<SimBone>();
        FindEndEffectorsOfType(state.RootBone, LimbType.Leg, legs);
        float maxLegLength = 0.1f;
        float avgLegLength = 0.1f;
        if (legs.Count > 0)
        {
            float sum = 0f;
            foreach (var leg in legs)
            {
                float len = CalculateChainLength(leg);
                if (len > maxLegLength) maxLegLength = len;
                sum += len;
            }
            avgLegLength = sum / legs.Count;
        }
        
        state.StepDistance = avgLegLength * 1.0f;
        state.StepHeight = avgLegLength * 0.25f;
        float bodyLength = (dna.SpineSegments * dna.SpineSegmentLength) + (dna.TailSegments * dna.TailSegmentLength);
        if (bodyLength < 0.1f) bodyLength = 0.1f;
        float baseSpeed = (avgLegLength * 1.0f) + (bodyLength * 0.2f);
        float legCountMultiplier = 1f + (legs.Count * 0.05f);
        state.WalkSpeed = Mathf.Max(0.5f, baseSpeed * legCountMultiplier * 0.6f);
        state.TurnSpeed = 60f / (1f + bodyLength);
        
        float postureCost = 0f;
        if (legs.Count == 2)
        {
            float frontMass = dna.SpineSegments * dna.SpineSegmentLength;
            float rearMass = dna.TailSegments * dna.TailSegmentLength;
            float balanceRatio = Mathf.Abs(frontMass - rearMass);
            float horizontalPenalty = (90f - dna.PosturePitch) / 90f;
            postureCost = balanceRatio * horizontalPenalty * 15f;
        }
        else if (legs.Count > 2)
        {
            float uprightPenalty = dna.PosturePitch / 90f;
            postureCost = uprightPenalty * 20f;
        }
        state.EnergyCost = (state.Mass * 0.2f) + (state.WalkSpeed * 2f) + postureCost;
    }

    private static void ComputeModelSpace(SimBone bone, Vector3 parentPos, Quaternion parentRot, List<Vector3> allPoints)
    {
        Vector3 boneStart = parentPos + parentRot * bone.LocalPosition;
        Quaternion boneRot = parentRot * bone.LocalRotation;
        Vector3 boneEnd = boneStart + boneRot * (Vector3.forward * bone.Length);
        allPoints.Add(boneStart);
        allPoints.Add(boneEnd);
        foreach (var child in bone.Children)
        {
            ComputeModelSpace(child, boneStart, boneRot, allPoints);
        }
    }

    private static float CalculateTotalBoneLength(SimBone bone)
    {
        float sum = bone.Length;
        foreach (var child in bone.Children) sum += CalculateTotalBoneLength(child);
        return sum;
    }

    private static void FindEndEffectorsOfType(SimBone bone, LimbType type, List<SimBone> list)
    {
        if (bone.IsEndEffector && bone.Type == type) list.Add(bone);
        foreach (var child in bone.Children) FindEndEffectorsOfType(child, type, list);
    }

    private static float CalculateChainLength(SimBone endEffector)
    {
        float len = 0f;
        SimBone curr = endEffector;
        while (curr != null && !curr.Name.Contains("Spine_") && curr.Parent != null)
        {
            len += curr.Length;
            curr = curr.Parent;
        }
        return len;
    }

    public static SimBone GenerateSkeleton(BodyPlanDNA dna)
    {
        SimBone root = new SimBone
        {
            Name = "Spine_0 (Root)",
            Type = LimbType.Leg,
            LocalPosition = Vector3.zero,
            LocalRotation = Quaternion.identity,
            Length = dna.SpineSegmentLength
        };
        
        List<SimBone> spineBones = new List<SimBone> { root };
        SimBone currentSpine = root;
        
        for (int i = 1; i < dna.SpineSegments; i++)
        {
            SimBone nextSpine = new SimBone
            {
                Name = $"Spine_{i}",
                Type = LimbType.Leg,
                LocalPosition = new Vector3(0, 0, currentSpine.Length),
                LocalRotation = Quaternion.identity,
                Length = dna.SpineSegmentLength
            };
            currentSpine.Children.Add(nextSpine);
            nextSpine.Parent = currentSpine;
            spineBones.Add(nextSpine);
            currentSpine = nextSpine;
        }
        
        SimBone lastSpine = spineBones[spineBones.Count - 1];
        SimBone currentTail = lastSpine;
        
        for (int i = 1; i <= dna.TailSegments; i++)
        {
            SimBone nextTail = new SimBone
            {
                Name = $"Tail_{i}",
                Type = LimbType.Tail,
                LocalPosition = (i == 1) ? Vector3.zero : new Vector3(0, 0, currentTail.Length),
                LocalRotation = (i == 1) ? Quaternion.Euler(0, 180, 0) : Quaternion.identity,
                Length = dna.TailSegmentLength
            };
            currentTail.Children.Add(nextTail);
            nextTail.Parent = currentTail;
            currentTail = nextTail;
        }
        
        if (dna.Limbs != null)
        {
            foreach (var limbDna in dna.Limbs)
            {
                int segmentIndex = Mathf.Clamp(limbDna.AttachedSegmentIndex, 0, spineBones.Count - 1);
                SimBone attachBone = spineBones[segmentIndex];
                string baseName = limbDna.Type.ToString();
                
                // If it's a Head or has zero attachment spacing, it should always be a single central appendage
                bool isCentral = (limbDna.Type == LimbType.Head || limbDna.AttachmentSpacing == 0f);

                if (isCentral)
                {
                    GenerateLimbBranch(limbDna, attachBone, baseName,
                        Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll),
                        Vector3.zero);
                    continue; 
                }
                
                switch (dna.Symmetry)
                {
                    case SymmetryType.Bilateral:
                        GenerateLimbBranch(limbDna, attachBone, "L_" + baseName,
                            Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll),
                            new Vector3(-limbDna.AttachmentSpacing * 0.5f, 0, 0));
                        GenerateLimbBranch(limbDna, attachBone, "R_" + baseName,
                            Quaternion.Euler(limbDna.Pitch, -limbDna.Yaw, -limbDna.Roll),
                            new Vector3(limbDna.AttachmentSpacing * 0.5f, 0, 0));
                        break;
                    case SymmetryType.Radial:
                        for (int r = 0; r < dna.RadialCount; r++)
                        {
                            float angle = (360f / dna.RadialCount) * r;
                            Quaternion radialRot = Quaternion.Euler(0, angle, 0);
                            Quaternion finalRot = radialRot * Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll);
                            Vector3 offset = radialRot * new Vector3(0, 0, limbDna.AttachmentSpacing);
                            GenerateLimbBranch(limbDna, attachBone, $"{baseName}_{r}", finalRot, offset);
                        }
                        break;
                    case SymmetryType.Asymmetrical:
                        GenerateLimbBranch(limbDna, attachBone, baseName,
                            Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll),
                            Vector3.zero);
                        break;
                }
            }
        }
        return root;
    }

    private static void GenerateLimbBranch(LimbDNA limbDna, SimBone parentBone, string prefix, Quaternion rootRot, Vector3 rootOffset)
    {
        SimBone currentParent = parentBone;
        for (int i = 0; i < limbDna.JointCount; i++)
        {
            float len = (limbDna.BoneLengths != null && i < limbDna.BoneLengths.Length) ? limbDna.BoneLengths[i] : 1.0f;
            Quaternion localRot = Quaternion.identity;
            Vector3 localPos = new Vector3(0, 0, currentParent.Length);
            
            if (i == 0)
            {
                localRot = rootRot;
                localPos = rootOffset;
            }
            
            SimBone joint = new SimBone
            {
                Name = $"{prefix}_J{i}",
                Type = limbDna.Type,
                LocalPosition = localPos,
                LocalRotation = localRot,
                Length = len
            };
            
            currentParent.Children.Add(joint);
            joint.Parent = currentParent;
            currentParent = joint;
        }
        
        if (limbDna.ChildLimbs != null)
        {
            foreach (var childLimb in limbDna.ChildLimbs)
            {
                GenerateLimbBranch(childLimb, currentParent, $"{prefix}_Sub", Quaternion.Euler(childLimb.Pitch, childLimb.Yaw, childLimb.Roll), Vector3.zero);
            }
        }
    }
}
