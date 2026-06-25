using System.Collections.Generic;
using UnityEngine;

public class SimBone
{
    public string Name;
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public float Length;
    
    public SimBone Parent;
    public List<SimBone> Children = new List<SimBone>();
    public bool IsEndEffector => Children.Count == 0;
}

public static class SkeletonGenerator
{
    public static SimBone GenerateSkeleton(BodyPlanDNA dna)
    {
        // 1. Generate the Spine
        SimBone root = new SimBone 
        { 
            Name = "Spine_0 (Root)", 
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
                LocalPosition = new Vector3(0, 0, currentSpine.Length), // Attach to end of previous segment
                LocalRotation = Quaternion.identity,
                Length = dna.SpineSegmentLength
            };
            currentSpine.Children.Add(nextSpine);
            nextSpine.Parent = currentSpine;
            spineBones.Add(nextSpine);
            currentSpine = nextSpine;
        }

        // 2. Grow Limbs based on Symmetry Topology
        if (dna.Limbs != null)
        {
            foreach (var limbDna in dna.Limbs)
            {
                int segmentIndex = Mathf.Clamp(limbDna.AttachedSegmentIndex, 0, spineBones.Count - 1);
                SimBone attachBone = spineBones[segmentIndex];

                switch (dna.Symmetry)
                {
                    case SymmetryType.Bilateral:
                        // Left Limb
                        GenerateLimbBranch(limbDna, attachBone, "L_Limb", Quaternion.identity);
                        // Right Limb (Mirrored Yaw and Roll)
                        Quaternion mirrorRot = Quaternion.Euler(0, 180, 0); 
                        GenerateLimbBranch(limbDna, attachBone, "R_Limb", mirrorRot);
                        break;

                    case SymmetryType.Radial:
                        for (int r = 0; r < dna.RadialCount; r++)
                        {
                            float angle = (360f / dna.RadialCount) * r;
                            Quaternion radialRot = Quaternion.Euler(0, angle, 0);
                            GenerateLimbBranch(limbDna, attachBone, $"Radial_{r}", radialRot);
                        }
                        break;

                    case SymmetryType.Asymmetrical:
                        GenerateLimbBranch(limbDna, attachBone, "Limb", Quaternion.identity);
                        break;
                }
            }
        }

        return root;
    }

    private static void GenerateLimbBranch(LimbDNA limbDna, SimBone parentBone, string prefix, Quaternion symmetryOffset)
    {
        SimBone currentParent = parentBone;

        for (int i = 0; i < limbDna.JointCount; i++)
        {
            float len = (limbDna.BoneLengths != null && i < limbDna.BoneLengths.Length) ? limbDna.BoneLengths[i] : 1.0f;
            Quaternion localRot = Quaternion.identity;

            if (i == 0)
            {
                // First joint absorbs the DNA angles and symmetry offset
                Quaternion dnaRot = Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll);
                localRot = symmetryOffset * dnaRot;
            }

            SimBone joint = new SimBone
            {
                Name = $"{prefix}_J{i}",
                LocalPosition = i == 0 ? Vector3.zero : new Vector3(0, 0, currentParent.Length),
                LocalRotation = localRot,
                Length = len
            };

            currentParent.Children.Add(joint);
            joint.Parent = currentParent;
            currentParent = joint;
        }

        // Recursively generate sub-limbs (e.g., fingers/claws) at the end effector
        if (limbDna.ChildLimbs != null)
        {
            foreach (var childLimb in limbDna.ChildLimbs)
            {
                GenerateLimbBranch(childLimb, currentParent, $"{prefix}_Sub", Quaternion.identity);
            }
        }
    }
}
