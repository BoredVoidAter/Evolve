using System.Collections.Generic;
using UnityEngine;

public enum SymmetryType
{
    Bilateral,
    Radial,
    Asymmetrical
}

public enum LimbType
{
    Leg,
    Manipulator,
    Horn,
    Tentacle,
    Tail,
    Head
}

[System.Serializable]
public struct LimbDNA
{
    public LimbType Type;
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
    public int TailSegments;
    public float TailSegmentLength;
    public float PosturePitch;
    public float SpineStiffness;
    public List<LimbDNA> Limbs;
}

[System.Serializable]
public struct MorphogenesisDNA
{
    public float GlobalGrowthRate;
    public float MaturityAge;
}

[System.Serializable]
public struct TissueDNA
{
    public float MuscleMass;
    public float FatMass;
    public float ArmorMass;
}

[System.Serializable]
public struct SurfaceFeatureDNA { }

[System.Serializable]
public struct MembraneDNA
{
    public float WebbingAmount;
    public float Elasticity;
    public float Thickness;
}

[System.Serializable]
public struct OrganDNA
{
    public string PrefabID;
    public int AttachedSegmentIndex;
}

[System.Serializable]
public struct SkinDNA
{
    public Texture2D PatternMask;
    public Color PrimaryColor;
    public Color SecondaryColor;
}

[System.Serializable]
public struct BrainDNA { }

[System.Serializable]
public struct AnimalDNA
{
    public BodyPlanDNA BodyPlan;
    public MorphogenesisDNA Morphogenesis;
    public TissueDNA Tissue;
    public SurfaceFeatureDNA Features;
    public MembraneDNA Membranes;
    public List<OrganDNA> Organs;
    public SkinDNA Skin;
    public BrainDNA Brain;
}
