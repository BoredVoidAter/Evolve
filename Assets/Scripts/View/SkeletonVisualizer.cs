using System.Collections.Generic;
using UnityEngine;

// Associates a Unity View GameObject with its backing Simulation Data
public class BoneTag : MonoBehaviour
{
    public SimBone bone;
}

public class SkeletonVisualizer : MonoBehaviour
{
    public Material boneMaterial;
    private List<(Transform boneTransform, float length, LineRenderer lr, TextMesh label)> _renderData = new List<(Transform, float, LineRenderer, TextMesh)>();
    
    public void BuildSkeletonView(SimBone rootSimBone)
    {
        if (boneMaterial == null)
            boneMaterial = new Material(Shader.Find("Sprites/Default"));
            
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            child.parent = null;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        
        _renderData.Clear();
        BuildBoneRecursive(rootSimBone, transform);
    }
    
    private void BuildBoneRecursive(SimBone simBone, Transform parentTransform)
    {
        GameObject boneObj = new GameObject(simBone.Name);
        boneObj.transform.SetParent(parentTransform);
        boneObj.transform.localPosition = simBone.LocalPosition;
        boneObj.transform.localRotation = simBone.LocalRotation;
        
        BoneTag tag = boneObj.AddComponent<BoneTag>();
        tag.bone = simBone;

        LineRenderer lr = boneObj.AddComponent<LineRenderer>();
        lr.material = boneMaterial;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.02f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        
        Color boneColor = Color.cyan;
        if (simBone.Type == LimbType.Manipulator) boneColor = Color.green;
        if (simBone.Type == LimbType.Tentacle) boneColor = new Color(0.8f, 0f, 1f);
        if (simBone.Type == LimbType.Horn) boneColor = Color.gray;
        if (simBone.IsEndEffector && simBone.Type == LimbType.Leg) boneColor = Color.red;
        
        lr.startColor = boneColor;
        lr.endColor = Color.white;
        
        GameObject textObj = new GameObject("InfoLabel");
        textObj.transform.SetParent(boneObj.transform);
        textObj.transform.localPosition = new Vector3(0, 0.2f, simBone.Length * 0.5f);
        
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = $"{simBone.Name}\n{simBone.Type}\nLen: {simBone.Length:F1}";
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.yellow;
        
        _renderData.Add((boneObj.transform, simBone.Length, lr, tm));
        
        if (simBone.IsEndEffector)
        {
            GameObject tipObj = new GameObject(simBone.Name + "_Tip");
            tipObj.transform.SetParent(boneObj.transform);
            tipObj.transform.localPosition = new Vector3(0, 0, simBone.Length);
            tipObj.transform.localRotation = Quaternion.identity;
            
            BoneTag tipTag = tipObj.AddComponent<BoneTag>();
            tipTag.bone = simBone;
        }
        
        foreach (var child in simBone.Children)
        {
            BuildBoneRecursive(child, boneObj.transform);
        }
    }
    
    private void LateUpdate()
    {
        foreach (var data in _renderData)
        {
            if (data.boneTransform != null)
            {
                data.lr.SetPosition(0, data.boneTransform.position);
                data.lr.SetPosition(1, data.boneTransform.TransformPoint(Vector3.forward * data.length));
                if (Camera.main != null)
                    data.label.transform.rotation = Quaternion.LookRotation(data.label.transform.position - Camera.main.transform.position);
            }
        }
    }
}
