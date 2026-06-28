using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(SkeletonVisualizer))]
[RequireComponent(typeof(ProceduralLocomotion))]
[RequireComponent(typeof(CreatureFleshBuilder))]
[RequireComponent(typeof(UIDocument))]
public class Phase2Demo : MonoBehaviour
{
    public enum CreatureType { Biped, Ape, Spider, Centipede, Starfish, TentacleAlien }
    
    [Header("Demo Settings")]
    public CreatureType currentCreature = CreatureType.Biped;
    public StyleSheet customStyleSheet;
    public Material floorMaterial;
    public Material fleshMaterial;
    
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
            fleshMaterial = new Material(Shader.Find("Standard"));
            fleshMaterial.color = new Color(0.8f, 0.4f, 0.3f);
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
                                   $"SPEED: {_simState.WalkSpeed:F2} m/s\n" +
                                   $"STRIDE: {_simState.StepDistance:F2} m\n" +
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
        panel.style.width = 320;
        panel.style.paddingTop = 20;
        panel.style.paddingBottom = 20;
        panel.style.paddingLeft = 20;
        panel.style.paddingRight = 20;
        
        Label title = new Label("PHASE 2: MORPHOGENESIS");
        title.AddToClassList("panel-header");
        title.style.fontSize = 28;
        panel.Add(title);
        
        VisualElement divider = new VisualElement();
        divider.AddToClassList("section-divider");
        panel.Add(divider);
        
        panel.Add(CreateButton("BIPED (T-REX)", CreatureType.Biped));
        panel.Add(CreateButton("APE", CreatureType.Ape));
        panel.Add(CreateButton("SPIDER", CreatureType.Spider));
        panel.Add(CreateButton("CENTIPEDE", CreatureType.Centipede));
        panel.Add(CreateButton("STARFISH", CreatureType.Starfish));
        panel.Add(CreateButton("TENTACLE ALIEN", CreatureType.TentacleAlien));
        
        VisualElement divider2 = new VisualElement();
        divider2.AddToClassList("section-divider");
        divider2.style.marginTop = 20;
        panel.Add(divider2);
        
        _statsLabel = new Label("STATS...");
        _statsLabel.style.color = new Color(0.18f, 0.82f, 0.9f);
        _statsLabel.style.fontSize = 20;
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
        btn.style.marginTop = 10;
        btn.style.fontSize = 18;
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
        switch (currentCreature)
        {
            case CreatureType.Biped: dna.BodyPlan = GetBipedDNA(); break;
            case CreatureType.Ape: dna.BodyPlan = GetApeDNA(); break;
            case CreatureType.Spider: dna.BodyPlan = GetSpiderDNA(); break;
            case CreatureType.Centipede: dna.BodyPlan = GetCentipedeDNA(); break;
            case CreatureType.Starfish: dna.BodyPlan = GetStarfishDNA(); break;
            case CreatureType.TentacleAlien: dna.BodyPlan = GetTentacleAlienDNA(); break;
        }
        
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
            SpineSegments = 3,
            SpineSegmentLength = 1.0f,
            TailSegments = 4,
            TailSegmentLength = 0.8f,
            PosturePitch = 15f,
            SpineStiffness = 0.4f,
            Limbs = new List<LimbDNA> {
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 2, BoneLengths = new float[] { 0.5f, 0.4f }, Pitch = -20, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
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
                new LimbDNA { Type = LimbType.Head, AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 0.5f, 0.4f }, Pitch = -45, Yaw = 0, Roll = 0, AttachmentSpacing = 0f },
                new LimbDNA { Type = LimbType.Tentacle, AttachedSegmentIndex = 0, JointCount = 5, BoneLengths = new float[] { 0.6f, 0.5f, 0.4f, 0.3f, 0.2f }, Pitch = 15, Yaw = 0, Roll = 0, AttachmentSpacing = 0.5f },
                new LimbDNA { Type = LimbType.Horn, AttachedSegmentIndex = 1, JointCount = 1, BoneLengths = new float[] { 0.8f }, Pitch = -45, Yaw = 0, Roll = 0, AttachmentSpacing = 0.3f }
            }
        };
    }

    #endregion
}
