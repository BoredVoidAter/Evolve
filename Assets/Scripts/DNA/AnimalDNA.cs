using System.Collections.Generic;
using UnityEngine;

public enum SymmetryType
{
    Bilateral,
    Radial,
    Asymmetrical
}

[System.Serializable]
public struct LimbDNA
{
    public int AttachedSegmentIndex;
    public int JointCount;
    public float[] BoneLengths;
    public float Pitch;
    public float Yaw;
    public float Roll;
    public float AttachmentSpacing;
    public List<LimbDNA> ChildLimbs;
}

[System.Serializable]
public struct BodyPlanDNA
{
    public SymmetryType Symmetry;
    public int RadialCount;
    public int SpineSegments;
    public float SpineSegmentLength;
    public List<LimbDNA> Limbs;
}

[System.Serializable]
public struct AnimalDNA
{
    public BodyPlanDNA BodyPlan;
}
