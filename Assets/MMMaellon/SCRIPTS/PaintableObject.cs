
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


#if !COMPILER_UDONSHARP && UNITY_EDITOR
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UdonSharpEditor;
using System.Collections.Immutable;
#endif

#if !COMPILER_UDONSHARP && UNITY_EDITOR
[CustomEditor(typeof(PaintableObject))]
public class GunManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button(new GUIContent("Setup")) && Selection.activeGameObject != null)
        {
            PaintableObject paintable = Selection.activeGameObject.GetComponent<PaintableObject>();
            paintable.Setup();
        }
        if (GUILayout.Button(new GUIContent("SFX Setup")) && Selection.activeGameObject != null)
        {
            sfx sfx_source = GameObject.FindObjectOfType<sfx>();
            foreach (PaintableObject paintable in GameObject.FindObjectsOfType<PaintableObject>())
            {
                SerializedObject serialized = new SerializedObject(paintable);
                serialized.FindProperty("sfx_source").objectReferenceValue = sfx_source;
                serialized.ApplyModifiedProperties();
            }
            foreach (PaintBrush brush in GameObject.FindObjectsOfType<PaintBrush>())
            {
                SerializedObject serialized = new SerializedObject(brush);
                serialized.FindProperty("sfx_source").objectReferenceValue = sfx_source;
                serialized.ApplyModifiedProperties();
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        EditorGUILayout.Space();
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        EditorGUILayout.Space();
        base.OnInspectorGUI();
    }
}
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PaintableObject : UdonSharpBehaviour
{
    public bool instant = false;
    public bool colorAfterComplete = false;
    public sfx sfx_source;
    public MeshRenderer mesh;
    public Animator painted_animator;
    public string painted_animator_parameter_name = "painted";
    public Transform initial_splat;
    [ColorUsageAttribute(true, true)] public Color color = Color.white;
    [System.NonSerialized] public Vector3[] splats = new Vector3[16];//Only 16 because first splat is permanent - It lets players know what color they need
    [UdonSynced(UdonSyncMode.None), FieldChangeCallbackAttribute(nameof(complete))] public bool _complete = false;
    private float maxSize;

    public bool complete{
        get => _complete;
        set
        {
            _complete = value;
            if (complete)
            {
                Debug.LogWarning("PAINTED");
                _GrowSplat();
                if (painted_animator != null)
                {
                    painted_animator.SetBool(painted_animator_parameter_name, true);
                }
            }
            else
            {
                Debug.LogWarning("NOT PAINTED");
                _UnSplat();
                if (painted_animator != null)
                {
                    painted_animator.SetBool(painted_animator_parameter_name, false);
                }
            }
        }
    }
    private float splatSize = 0.2f;
    void Start()
    {
        complete = complete;
        mesh.material.color = color;
        if (initial_splat != null)
        {
            mesh.material.SetVector("_s0", mesh.transform.InverseTransformPoint(initial_splat.position));
        } else
        {
            mesh.material.SetVector("_s0", new Vector3(999, 999, 999));
        }
        maxSize = mesh.bounds.extents.magnitude * 2;
    }


    public void Setup()
    {
        mesh = GetComponent<MeshRenderer>();
        painted_animator = GetComponent<Animator>();
        if (mesh != null && mesh.material != null && mesh.material.color != null)
        {
            color = mesh.material.color;
        }
    }

    public void Splat(Vector3 newSplat, Color newColor)
    {
        if (complete)
        {
            return;
        }
        float oldH;
        float oldS;
        float oldV;
        float newH;
        float newS;
        float newV;
        Color.RGBToHSV(mesh.material.color, out oldH, out oldS, out oldV);
        Color.RGBToHSV(newColor, out newH, out newS, out newV);
        if (Mathf.Abs(oldH - newH) < 0.066f || Mathf.Abs(oldH - newH) > 1 - 0.1f && Mathf.Abs(oldS - newS) < 0.75f && Mathf.Abs(oldV - newV) < 0.5f)//strict on hue, lenient on saturation and value
        {
            if (!instant)
            {
                Vector3 localTransform = mesh.transform.InverseTransformPoint(newSplat);
                for (int i = 0; i < splats.Length; i++)
                {
                    if (splats[i] == null || splats[i] == new Vector3(999, 999, 999))
                    {
                        splats[i] = localTransform;

                        mesh.material.SetVector("_s" + (1 + i), localTransform);
                        if (sfx_source != null)
                        {
                            sfx_source.PlayPaint(newSplat);
                        }
                        return;
                    }
                    else if ((mesh.transform.TransformPoint(splats[i]) - newSplat).magnitude < Mathf.Min(0.1f, maxSize / 32))
                    {
                        Debug.LogWarning("too close!");
                        Debug.LogWarning("world distance " + (mesh.transform.TransformPoint(splats[i]) - localTransform).magnitude);
                        Debug.LogWarning("min: " + Mathf.Min(0.1f, maxSize / 32));
                        return;
                    }
                }
            }

            //all splats filled
            // SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.Owner, nameof(Complete));
            Complete();
            _GrowSplat();
        }
    }

    public void Complete()
    {
        complete = true;
        // RequestSerialization();

        if (sfx_source != null)
        {
            sfx_source.PlayComplete(mesh.transform.position);
        }
    }
    public void UnComplete()
    {
        complete = false;
        // RequestSerialization();
    }

    public void _GrowSplat()
    {
        if (splatSize < maxSize)
        {
            splatSize = Mathf.Lerp(splatSize, maxSize + 0.001f, 0.0025f);
            mesh.material.SetFloat("_SplatSize", splatSize);
            SendCustomEventDelayedFrames(nameof(_GrowSplat), 1);
        } else
        {
            mesh.material.SetFloat("_SplatSize", Mathf.Max(splatSize, 1001));
        }
    }

    public void _UnSplat()
    {
        splatSize = 0.2f;
        Vector3 defaultVector = new Vector3(999, 999, 999);
        for (int i = 0; i < splats.Length; i++)
        {
            splats[i] = defaultVector;
            mesh.material.SetVector("_s" + (i + 1), defaultVector);
        }
        mesh.material.SetFloat("_SplatSize", splatSize);
    }

    // public override void OnDeserialization()
    // {
    //     if (complete)
    //     {
    //         _GrowSplat();
    //     } else
    //     {
    //         _UnSplat();
    //     }
    // }
}
