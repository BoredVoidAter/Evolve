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
                LocalPosition = new Vector3(0, 0, currentSpine.Length),
                LocalRotation = Quaternion.identity,
                Length = dna.SpineSegmentLength
            };

            currentSpine.Children.Add(nextSpine);
            nextSpine.Parent = currentSpine;
            spineBones.Add(nextSpine);

            currentSpine = nextSpine;
        }

        if (dna.Limbs != null)
        {
            foreach (var limbDna in dna.Limbs)
            {
                int segmentIndex = Mathf.Clamp(limbDna.AttachedSegmentIndex, 0, spineBones.Count - 1);
                SimBone attachBone = spineBones[segmentIndex];

                switch (dna.Symmetry)
                {
                    case SymmetryType.Bilateral:
                        GenerateLimbBranch(limbDna, attachBone, "L_Limb", 
                            Quaternion.Euler(limbDna.Pitch, limbDna.Yaw, limbDna.Roll), 
                            new Vector3(-limbDna.AttachmentSpacing * 0.5f, 0, 0));
                            
                        GenerateLimbBranch(limbDna, attachBone, "R_Limb", 
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
                            GenerateLimbBranch(limbDna, attachBone, $"Radial_{r}", finalRot, offset);
                        }
                        break;

                    case SymmetryType.Asymmetrical:
                        GenerateLimbBranch(limbDna, attachBone, "Limb", 
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
