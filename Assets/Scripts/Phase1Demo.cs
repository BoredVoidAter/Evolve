using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(SkeletonVisualizer))]
[RequireComponent(typeof(ProceduralLocomotion))]
[RequireComponent(typeof(UIDocument))]
public class Phase1Demo : MonoBehaviour
{
    public enum CreatureType { Biped, Spider, Centipede, Starfish }

    [Header("Demo Settings")]
    public CreatureType currentCreature = CreatureType.Biped;
    public StyleSheet customStyleSheet;
    public Material floorMaterial;

    private SkeletonVisualizer _visualizer;
    private ProceduralLocomotion _locomotion;
    private GameObject _demoFloor;
    private UIDocument _uiDocument;
    private Camera _demoCamera;

    void Start()
    {
        _visualizer = GetComponent<SkeletonVisualizer>();
        _locomotion = GetComponent<ProceduralLocomotion>();
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

        BuildUI();
        CreateFloor();
        GenerateCreature();
    }

    private void LateUpdate()
    {
        if (_demoCamera != null && _demoCamera.gameObject.activeInHierarchy)
        {
            Vector3 targetCamPos = transform.position - Vector3.forward * 8f + Vector3.up * 5f;
            _demoCamera.transform.position = Vector3.Lerp(_demoCamera.transform.position, targetCamPos, Time.deltaTime * 4f);
            _demoCamera.transform.LookAt(transform.position + Vector3.up * 1f);
        }
    }

    private void BuildUI()
    {
        var root = _uiDocument.rootVisualElement;
        root.Clear();

        if (customStyleSheet != null)
            root.styleSheets.Add(customStyleSheet);

        VisualElement panel = new VisualElement();
        panel.AddToClassList("content-panel");
        panel.style.position = Position.Absolute;
        panel.style.top = 20;
        panel.style.left = 20;
        panel.style.width = 300;
        panel.style.paddingTop = 20;
        panel.style.paddingBottom = 20;
        panel.style.paddingLeft = 20;
        panel.style.paddingRight = 20;

        Label title = new Label("PHASE 1");
        title.AddToClassList("panel-header");
        title.style.fontSize = 28;
        panel.Add(title);

        VisualElement divider = new VisualElement();
        divider.AddToClassList("section-divider");
        panel.Add(divider);

        panel.Add(CreateButton("BIPED", CreatureType.Biped));
        panel.Add(CreateButton("SPIDER", CreatureType.Spider));
        panel.Add(CreateButton("CENTIPEDE", CreatureType.Centipede));
        panel.Add(CreateButton("STARFISH", CreatureType.Starfish));

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
        btn.style.fontSize = 20;
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
            if (floorMaterial != null)
            {
                r.material = floorMaterial;
            }
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
            case CreatureType.Spider: dna.BodyPlan = GetSpiderDNA(); break;
            case CreatureType.Centipede: dna.BodyPlan = GetCentipedeDNA(); break;
            case CreatureType.Starfish: dna.BodyPlan = GetStarfishDNA(); break;
        }

        transform.position = new Vector3(0, 2f, 0);
        transform.localRotation = Quaternion.identity;

        SimBone rootSkeleton = SkeletonGenerator.GenerateSkeleton(dna.BodyPlan);
        _visualizer.BuildSkeletonView(rootSkeleton);
        _locomotion.InitializeLocomotion();
    }

    #region DNA Presets

    private BodyPlanDNA GetBipedDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral, SpineSegments = 3, SpineSegmentLength = 1.0f,
            Limbs = new List<LimbDNA> {
                // Yaw of -20 angles the arms out to the sides slightly so they don't clip the hips
                new LimbDNA { AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.8f, 0.8f, 0.2f }, Pitch = 90, Yaw = -20, Roll = 0, AttachmentSpacing = 1.2f },
                // Yaw of 0 points the legs perfectly straight forward
                new LimbDNA { AttachedSegmentIndex = 2, JointCount = 3, BoneLengths = new float[] { 1.2f, 1.2f, 0.4f }, Pitch = 90, Yaw = 0, Roll = 0, AttachmentSpacing = 0.8f }
            }
        };
    }

    private BodyPlanDNA GetSpiderDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Bilateral, SpineSegments = 2, SpineSegmentLength = 0.8f,
            Limbs = new List<LimbDNA> {
                // Negative Yaws angle the left limbs progressively outward/backwards (Right limbs automatically mirror it)
                new LimbDNA { AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.2f, 1f }, Pitch = 20, Yaw = -40,  Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { AttachedSegmentIndex = 0, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.5f, 1f }, Pitch = 20, Yaw = -80,  Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { AttachedSegmentIndex = 1, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.5f, 1f }, Pitch = 20, Yaw = -100, Roll = 0, AttachmentSpacing = 0.6f },
                new LimbDNA { AttachedSegmentIndex = 1, JointCount = 3, BoneLengths = new float[] { 0.5f, 1.2f, 1f }, Pitch = 20, Yaw = -140, Roll = 0, AttachmentSpacing = 0.6f }
            }
        };
    }

    private BodyPlanDNA GetCentipedeDNA()
    {
        var limbs = new List<LimbDNA>();
        // -90 Yaw sweeps all centipede legs out perfectly sideways
        for (int i = 0; i < 15; i++) 
            limbs.Add(new LimbDNA { AttachedSegmentIndex = i, JointCount = 2, BoneLengths = new float[] { 0.4f, 0.6f }, Pitch = 45, Yaw = -90, Roll = 0, AttachmentSpacing = 0.5f });
            
        return new BodyPlanDNA { Symmetry = SymmetryType.Bilateral, SpineSegments = 15, SpineSegmentLength = 0.5f, Limbs = limbs };
    }

    private BodyPlanDNA GetStarfishDNA()
    {
        return new BodyPlanDNA {
            Symmetry = SymmetryType.Radial, RadialCount = 5, SpineSegments = 1, SpineSegmentLength = 0.1f,
            Limbs = new List<LimbDNA> { new LimbDNA { AttachedSegmentIndex = 0, JointCount = 4, BoneLengths = new float[] { 0.8f, 0.6f, 0.4f, 0.2f }, Pitch = 10, Yaw = 0, Roll = 0, AttachmentSpacing = 0.2f } }
        };
    }

    #endregion
}
