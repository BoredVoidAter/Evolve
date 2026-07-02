using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(SkeletonVisualizer))]
[RequireComponent(typeof(ProceduralLocomotion))]
[RequireComponent(typeof(CreatureFleshBuilder))]
[RequireComponent(typeof(UIDocument))]
public class Phase2Demo : MonoBehaviour
{
    public enum CreatureType { Biped, Ape, Spider, Centipede, Starfish, TentacleAlien, Lizard, Triceratops, Mammal }
    
    [Header("Demo Settings")]
    public CreatureType currentCreature = CreatureType.Biped;
    public StyleSheet customStyleSheet;
    public Material floorMaterial;
    public Material fleshMaterial;

    [Header("Textures (Phase 2 & 3)")]
    public Texture2D fluffyTexture;
    public Texture2D scalesTexture;
    
    private SkeletonVisualizer _visualizer;
    private ProceduralLocomotion _locomotion;
    private CreatureFleshBuilder _fleshBuilder;
    private SimCreatureState _simState;
    private GameObject _demoFloor;
    private UIDocument _uiDocument;
    private Camera _demoCamera;
    private Label _statsLabel;

    private float _cameraDistance = 8f;
    private float _cameraYaw = -30f;
    private float _cameraPitch = 20f;
    private Vector2 _lastMousePos;

    void Start()
    {
        _visualizer = GetComponent<SkeletonVisualizer>();
        _locomotion = GetComponent<ProceduralLocomotion>();
        _fleshBuilder = GetComponent<CreatureFleshBuilder>();
        _uiDocument = GetComponent<UIDocument>();
        _demoCamera = GetComponentInChildren<Camera>();
        
        #if UNITY_EDITOR
        // Auto-load textures robustly using AssetDatabase so file paths don't break
        if (fluffyTexture == null) 
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Fluffy t:Texture2D");
            if (guids.Length > 0) fluffyTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (scalesTexture == null) 
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Scales t:Texture2D");
            if (guids.Length > 0) scalesTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        #endif

        if (_demoCamera == null)
        {
            GameObject camObj = new GameObject("DemoCamera");
            camObj.transform.SetParent(this.transform.parent);
            _demoCamera = camObj.AddComponent<Camera>();
            _demoCamera.backgroundColor = new Color(0.1f, 0.15f, 0.2f);
            _demoCamera.clearFlags = CameraClearFlags.SolidColor;
        }
        
        if (fleshMaterial == null)
        {
            Shader toon = Shader.Find("Shader Graphs/SH_UniversalToon");
            fleshMaterial = new Material(toon != null ? toon : Shader.Find("Standard"));
            if (fleshMaterial.HasProperty("_Col1")) fleshMaterial.SetColor("_Col1", new Color(0.8f, 0.4f, 0.3f, 1f));
            if (fleshMaterial.HasProperty("_Color")) fleshMaterial.color = new Color(0.8f, 0.4f, 0.3f, 1f);
        }
        _fleshBuilder.fleshMaterial = fleshMaterial;
        
        BuildUI();
        CreateFloor();
        GenerateCreature();
    }

    private void Update()
    {
        if (_simState != null)
        {
            _simState.UpdateSimulation(Time.deltaTime);
            if (_statsLabel != null)
            {
                _statsLabel.text = $"MASS: {_simState.Mass:F1} kg\n" +
                                   $" - MUSCLE: {_simState.MuscleMass:F1} kg\n" +
                                   $" - FAT: {_simState.FatMass:F1} kg\n" +
                                   $" - ARMOR: {_simState.ArmorMass:F1} kg\n" +
                                   $" - FEATURES: {_simState.FeatureMass:F1} kg\n" +
                                   $"SPEED: {_simState.WalkSpeed:F2} m/s\n" +
                                   $"ENERGY COST: {_simState.EnergyCost:F1}/m";
            }
        }
    }

    private void LateUpdate()
    {
        if (_demoCamera != null && _demoCamera.gameObject.activeInHierarchy)
        {
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _cameraDistance -= Mathf.Sign(scroll) * 1.5f;
                    _cameraDistance = Mathf.Clamp(_cameraDistance, 2f, 30f);
                }

                Vector2 currentMousePos = mouse.position.ReadValue();
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    _lastMousePos = currentMousePos;
                }
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = currentMousePos - _lastMousePos;
                    _cameraYaw += delta.x * 0.3f;
                    _cameraPitch -= delta.y * 0.3f;
                    _cameraPitch = Mathf.Clamp(_cameraPitch, -10f, 85f);
                }
                _lastMousePos = currentMousePos;
            }

            Quaternion rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0);
            Vector3 targetCamPos = transform.position + Vector3.up * 1f + rotation * new Vector3(0, 0, -_cameraDistance);
            
            _demoCamera.transform.position = Vector3.Lerp(_demoCamera.transform.position, targetCamPos, Time.deltaTime * 10f);
            _demoCamera.transform.LookAt(transform.position + Vector3.up * 1f);
        }
    }

    private void BuildUI()
    {
        var root = _uiDocument.rootVisualElement;
        root.Clear();
        if (customStyleSheet != null) root.styleSheets.Add(customStyleSheet);
        
        VisualElement panel = new VisualElement();
        panel.AddToClassList("content-panel");
        panel.style.position = Position.Absolute;
        panel.style.top = 20;
        panel.style.left = 20;
        panel.style.width = 340;
        panel.style.paddingTop = 20;
        panel.style.paddingBottom = 20;
        panel.style.paddingLeft = 20;
        panel.style.paddingRight = 20;
        
        Label title = new Label("PHASE 2: MORPHOGENESIS");
        title.AddToClassList("panel-header");
        title.style.fontSize = 24;
        panel.Add(title);
        
        VisualElement divider = new VisualElement();
        divider.AddToClassList("section-divider");
        panel.Add(divider);
        
        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.maxHeight = 350;
        
        scroll.Add(CreateButton("BIPED (T-REX)", CreatureType.Biped));
        scroll.Add(CreateButton("APE", CreatureType.Ape));
        scroll.Add(CreateButton("SPIDER", CreatureType.Spider));
        scroll.Add(CreateButton("CENTIPEDE", CreatureType.Centipede));
        scroll.Add(CreateButton("STARFISH", CreatureType.Starfish));
        scroll.Add(CreateButton("TENTACLE ALIEN", CreatureType.TentacleAlien));
        scroll.Add(CreateButton("LIZARD (Scales/Ridge)", CreatureType.Lizard));
        scroll.Add(CreateButton("TRICERATOPS (Frill)", CreatureType.Triceratops));
        scroll.Add(CreateButton("MAMMAL (Fur)", CreatureType.Mammal));
        
        panel.Add(scroll);

        VisualElement divider2 = new VisualElement();
        divider2.AddToClassList("section-divider");
        divider2.style.marginTop = 10;
        panel.Add(divider2);
        
        _statsLabel = new Label("STATS...");
        _statsLabel.style.color = new Color(0.18f, 0.82f, 0.9f);
        _statsLabel.style.fontSize = 18;
        _statsLabel.style.marginTop = 10;
        panel.Add(_statsLabel);
        
        root.Add(panel);
    }

    private Button CreateButton(string text, CreatureType type)
    {
        Button btn = new Button(() => {
            currentCreature = type;
            GenerateCreature();
        });
        btn.text = text;
        btn.AddToClassList("action-btn");
        btn.style.marginTop = 5;
        btn.style.fontSize = 16;
        return btn;
    }

    private void CreateFloor()
    {
        if (_demoFloor != null) return;
        _demoFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        _demoFloor.name = "EVOLVE_Demo_Floor";
        _demoFloor.transform.SetParent(null);
        _demoFloor.transform.position = new Vector3(0, 0, 0);
        _demoFloor.transform.localScale = new Vector3(50, 1, 50);
        Renderer r = _demoFloor.GetComponent<Renderer>();
        if (r != null)
        {
            if (floorMaterial != null) r.material = floorMaterial;
            else
            {
                Material floorMat = new Material(Shader.Find("Standard"));
                floorMat.color = new Color(0.15f, 0.2f, 0.15f);
                r.material = floorMat;
            }
        }
    }

    private void GenerateCreature()
    {
        AnimalDNA dna = new AnimalDNA();
        dna.Morphogenesis = new MorphogenesisDNA { GlobalGrowthRate = 1f, Regions = new List<BodyRegionDNA>() };

        switch (currentCreature)
        {
            case CreatureType.Biped: 
                dna.BodyPlan = GetBipedDNA(); 
                // Neck set to 0.7 to retain thickness while keeping the head large
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Head, RelativeSize = 1.8f, GrowthRate = 1f });
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Neck, RelativeSize = 0.7f, GrowthRate = 1f });
                dna.Tissue = new TissueDNA { MuscleMass = 5f, FatMass = 0f, ArmorMass = 0f };
                dna.Features = new SurfaceFeatureDNA { Type = SurfaceFeatureType.Scale, Density = 1f, Length = 0.5f, Thickness = 0.15f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.2f, 0.6f, 0.3f, 1f), SecondaryColor = new Color(0.1f, 0.4f, 0.2f, 1f) };
                break;
            case CreatureType.Ape: 
                dna.BodyPlan = GetApeDNA(); 
                dna.Tissue = new TissueDNA { MuscleMass = 3f, FatMass = 0f, ArmorMass = 0f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.4f, 0.3f, 0.2f, 1f), SecondaryColor = new Color(0.2f, 0.15f, 0.1f, 1f) };
                break;
            case CreatureType.Spider: 
                dna.BodyPlan = GetSpiderDNA(); 
                dna.Tissue = new TissueDNA { MuscleMass = 1f, FatMass = 2f, ArmorMass = 0f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.1f, 0.1f, 0.1f, 1f), SecondaryColor = new Color(0.8f, 0.1f, 0.1f, 1f) };
                break;
            case CreatureType.Centipede: 
                dna.BodyPlan = GetCentipedeDNA(); 
                dna.Tissue = new TissueDNA { MuscleMass = 2f, FatMass = 0f, ArmorMass = 8f };
                dna.Features = new SurfaceFeatureDNA { Type = SurfaceFeatureType.Plate, Density = 1f, Length = 0.4f, Thickness = 0.1f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.6f, 0.2f, 0.1f, 1f), SecondaryColor = new Color(0.9f, 0.5f, 0.1f, 1f) };
                break;
            case CreatureType.Starfish: 
                dna.BodyPlan = GetStarfishDNA();
                dna.Membranes = new MembraneDNA { WebbingAmount = 1f }; 
                dna.Tissue = new TissueDNA { MuscleMass = 2f, FatMass = 2f, ArmorMass = 0f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.9f, 0.4f, 0.6f, 1f), SecondaryColor = new Color(0.8f, 0.2f, 0.4f, 1f) };
                break;
            case CreatureType.TentacleAlien: 
                dna.BodyPlan = GetTentacleAlienDNA(); 
                dna.Membranes = new MembraneDNA { WebbingAmount = 1f }; 
                dna.Tissue = new TissueDNA { MuscleMass = 3f, FatMass = 1f, ArmorMass = 0f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.3f, 0.1f, 0.7f, 1f), SecondaryColor = new Color(0.1f, 0.8f, 0.6f, 1f) };
                break;
            case CreatureType.Lizard:
                dna.BodyPlan = GetLizardDNA();
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Tail, RelativeSize = 0.8f, GrowthRate = 1f });
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Neck, RelativeSize = 0.6f, GrowthRate = 1f });
                dna.Features = new SurfaceFeatureDNA { Type = SurfaceFeatureType.Ridge, Density = 1f, Length = 0.6f, Thickness = 0.05f };
                dna.Tissue = new TissueDNA { MuscleMass = 2f, FatMass = 1f, ArmorMass = 1f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.1f, 0.5f, 0.2f, 1f), SecondaryColor = new Color(0.2f, 0.7f, 0.3f, 1f) };
                break;
            case CreatureType.Triceratops:
                dna.BodyPlan = GetTriceratopsDNA();
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Head, RelativeSize = 2.0f, GrowthRate = 1f });
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Neck, RelativeSize = 0.7f, GrowthRate = 1f });
                dna.Features = new SurfaceFeatureDNA { Type = SurfaceFeatureType.Frill, Density = 1f, Length = 0.8f, Thickness = 0.2f };
                dna.Tissue = new TissueDNA { MuscleMass = 4f, FatMass = 1.5f, ArmorMass = 5f };
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.5f, 0.4f, 0.3f, 1f), SecondaryColor = new Color(0.3f, 0.2f, 0.2f, 1f) };
                break;
            case CreatureType.Mammal:
                dna.BodyPlan = GetMammalDNA();
                dna.Morphogenesis.Regions.Add(new BodyRegionDNA { RegionType = BodyRegionType.Thorax, RelativeSize = 1.0f, GrowthRate = 1f });
                dna.Features = new SurfaceFeatureDNA { Type = SurfaceFeatureType.Fur, Density = 1f, Length = 0f, Thickness = 0f };
                dna.Tissue = new TissueDNA { MuscleMass = 2f, FatMass = 0f, ArmorMass = 0f }; 
                dna.Skin = new SkinDNA { PrimaryColor = new Color(0.6f, 0.4f, 0.2f, 1f), SecondaryColor = new Color(0.8f, 0.6f, 0.3f, 1f) };
                break;
        }

        // Texture Injector
        if (dna.Features.Type == SurfaceFeatureType.Fur && fluffyTexture != null) dna.Skin.PatternMask = fluffyTexture;
        if (dna.Features.Type == SurfaceFeatureType.Scale && scalesTexture != null) dna.Skin.PatternMask = scalesTexture;
        if (dna.Features.Type == SurfaceFeatureType.Ridge && scalesTexture != null) dna.Skin.PatternMask = scalesTexture;
        if (dna.Features.Type == SurfaceFeatureType.Frill && scalesTexture != null) dna.Skin.PatternMask = scalesTexture;
        
        transform.position = new Vector3(0, 2f, 0);
        transform.localRotation = Quaternion.identity;
        
        _locomotion.posturePitch = dna.BodyPlan.PosturePitch;
        _locomotion.spineStiffness = dna.BodyPlan.SpineStiffness;
        _simState = SkeletonGenerator.GenerateCreatureState(dna, transform.position);
        
        _visualizer.BuildSkeletonView(_simState.RootBone);
        _locomotion.InitializeLocomotion(_simState);
        _fleshBuilder.BuildFlesh(dna, transform);
    }

    #region DNA Presets

    private BodyPlanDNA GetBipedDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 3, SpineSegmentLength = 1.0f, TailSegments = 4, TailSegmentLength = 0.8f,
            PosturePitch = 15f, SpineStiffness = 0.4f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.7f, 0.6f, 0.5f }, Pitch = -20, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 2, JointCount = 3, BoneLengths = new float[] { 1.2f, 1.2f, 0.4f }, Pitch = 90, Yaw = -20, Roll = 0, AttachmentSpacing = 1.2f },
                new LimbDNA { Type = LimbType.Manipulator, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.4f, 0.4f, 0.2f }, Pitch = 45, Yaw = 0, Roll = 0, AttachmentSpacing = 0.8f }
            }
        };
    }

    private BodyPlanDNA GetApeDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 3, SpineSegmentLength = 0.8f, TailSegments = 0, TailSegmentLength = 0f,
            PosturePitch = 45f, SpineStiffness = 0.85f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.4f, 0.4f }, Pitch = -30, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 2, JointCount = 3, BoneLengths = new float[] { 0.6f, 0.6f, 0.2f }, Pitch = 90, Yaw = -30, Roll = 0, AttachmentSpacing = 1.0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.9f, 0.9f, 0.2f }, Pitch = 45, Yaw = -10, Roll = 0, AttachmentSpacing = 1.4f }
            }
        };
    }

    private BodyPlanDNA GetSpiderDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 2, SpineSegmentLength = 0.8f, TailSegments = 0, TailSegmentLength = 0f,
            PosturePitch = 0f, SpineStiffness = 0f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.3f, 0.2f }, Pitch = 0, Yaw = 0, Roll = 0, AttachmentSpacing = 0.2f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.2f, 1f }, Pitch = 20, Yaw = -40,  Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.5f, 1f }, Pitch = 20, Yaw = -80,  Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 1, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.5f, 1f }, Pitch = 20, Yaw = -100, Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 1, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.2f, 1f }, Pitch = 20, Yaw = -140, Roll = 0, AttachmentSpacing = 0.6f }
            }
        };
    }

    private BodyPlanDNA GetCentipedeDNA()
    {
        var limbs = new List<LimbDNA>();
        limbs.Add(new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.4f, 0.3f }, Pitch = 0, Yaw = 0, Roll = 0, AttachmentSpacing = 0f });
        for (int i = 0; i < 15; i++)
            limbs.Add(new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = i, JointCount = 2, BoneLengths = new float[] { 0.4f, 0.6f }, Pitch = 45, Yaw = -90, Roll = 0, AttachmentSpacing = 0.5f });
        
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral, SpineSegments = 15, SpineSegmentLength = 0.5f, TailSegments = 0, TailSegmentLength = 0f,
            PosturePitch = 0f, SpineStiffness = 0f, Limbs = limbs
        };
    }

    private BodyPlanDNA GetStarfishDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Radial, RadialCount = 5, SpineSegments = 1, SpineSegmentLength = 0.1f, TailSegments = 0, TailSegmentLength = 0f,
            PosturePitch = 0f, SpineStiffness = 0f,
            Limbs = new List<LimbDNA> { new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 4, BoneLengths = new float[] { 0.8f, 0.6f, 0.4f, 0.2f }, Pitch = 10, Yaw = 0, Roll = 0, AttachmentSpacing = 0.2f } }
        };
    }

    private BodyPlanDNA GetTentacleAlienDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Radial, RadialCount = 6, SpineSegments = 2, SpineSegmentLength = 0.5f, TailSegments = 0, TailSegmentLength = 0f,
            PosturePitch = 0f, SpineStiffness = 0f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 0.5f, 0.4f }, Pitch = -90, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Tentacle, AttachedSegmentIndex = 0, JointCount = 5, BoneLengths = new float[] { 0.6f, 0.5f, 0.4f, 0.3f, 0.2f }, Pitch = 15, Yaw = 0, Roll = 0, AttachmentSpacing = 0.5f }
            }
        };
    }

    private BodyPlanDNA GetLizardDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 4, SpineSegmentLength = 0.6f, TailSegments = 5, TailSegmentLength = 0.5f,
            PosturePitch = 0f, SpineStiffness = 0.2f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.4f, 0.3f }, Pitch = -10, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 1, JointCount = 3, BoneLengths = new float[] { 0.5f, 0.6f, 0.2f }, Pitch = 70, Yaw = -40, Roll = 0, AttachmentSpacing = 0.8f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 3, JointCount = 3, BoneLengths = new float[] { 0.6f, 0.7f, 0.2f }, Pitch = 70, Yaw = -30, Roll = 0, AttachmentSpacing = 0.8f }
            }
        };
    }

    private BodyPlanDNA GetTriceratopsDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 3, SpineSegmentLength = 1.0f, TailSegments = 3, TailSegmentLength = 0.7f,
            PosturePitch = -5f, SpineStiffness = 0.8f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.8f, 0.6f }, Pitch = 10, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.8f, 0.8f, 0.3f }, Pitch = 90, Yaw = -10, Roll = 0, AttachmentSpacing = 1.5f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 2, JointCount = 3, BoneLengths = new float[] { 1.0f, 1.0f, 0.3f }, Pitch = 90, Yaw = -10, Roll = 0, AttachmentSpacing = 1.5f },
                new LimbDNA { Type = LimbType.Horn, AttachedSegmentIndex = 0, JointCount = 1, BoneLengths = new float[] { 0.7f }, Pitch = -60, Yaw = -30, Roll = 0, AttachmentSpacing = 0.4f }
            }
        };
    }

    private BodyPlanDNA GetMammalDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral,
            SpineSegments = 4, SpineSegmentLength = 0.8f, TailSegments = 2, TailSegmentLength = 0.5f,
            PosturePitch = 0f, SpineStiffness = 0.5f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.5f, 0.4f }, Pitch = -20, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.7f, 0.7f, 0.2f }, Pitch = 90, Yaw = -10, Roll = 0, AttachmentSpacing = 0.8f },
                new LimbDNA { Type = LimbType.Leg, AttachedSegmentIndex = 3, JointCount = 3, BoneLengths = new float[] { 0.8f, 0.8f, 0.2f }, Pitch = 90, Yaw = -10, Roll = 0, AttachmentSpacing = 0.8f }
            }
        };
    }

    #endregion
}
