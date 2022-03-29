using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

[EditorTool("Annotate")]
public class AnnotateTool : EditorTool
{
    public float Width = 0.1f;
    private float ssWidth;
    public Color StrokeColor = Color.cyan * 0.7f;
    public Color PlaneColor = Color.cyan * 0.25f;
    private bool isDrawing;
    private Transform container;
    private LineRenderer currentLine;
    private Plane plane;
    private Ray ray;
    private float d;
    public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("editicon.sml");

    public override void OnToolGUI(EditorWindow window)
    {
        // base.OnToolGUI(window);
        var view = window as SceneView;
        HandleUtility.AddDefaultControl(-1);
        Event e = Event.current;
        if (!e.shift) return;
        plane.SetNormalAndPosition(-view.camera.transform.forward, view.pivot);
        // var up = view.camera.transform.up * HandleUtility.GetHandleSize(view.pivot);
        // var right = view.camera.transform.right * HandleUtility.GetHandleSize(view.pivot);

        ray = HandleUtility.GUIPointToWorldRay(Vector2.zero);
        plane.Raycast(ray, out d);
        var p0 = ray.GetPoint(d) + view.camera.transform.forward * 0.01f;
        ray = HandleUtility.GUIPointToWorldRay(Vector2.up * view.position.height);
        plane.Raycast(ray, out d);
        var p1 = ray.GetPoint(d) + view.camera.transform.forward * 0.01f;
        ray = HandleUtility.GUIPointToWorldRay(new Vector2(view.position.width, view.position.height));
        plane.Raycast(ray, out d);
        var p2 = ray.GetPoint(d) + view.camera.transform.forward * 0.01f;
        ray = HandleUtility.GUIPointToWorldRay(Vector2.right * view.position.width);
        plane.Raycast(ray, out d);
        var p3 = ray.GetPoint(d) + view.camera.transform.forward * 0.01f;

        var color = Handles.color;
        var zTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.color = PlaneColor;
        Handles.DrawAAConvexPolygon(
            p0, p1, p2, p3
        );
        Handles.color = color;
        Handles.zTest = zTest;
        if (e.isMouse)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    // Debug.Log("Start drawing");
                    ssWidth = HandleUtility.GetHandleSize(view.pivot) * Width;
                    AddLine();
                    currentLine.positionCount = 1;
                    ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    plane.Raycast(ray, out d);
                    currentLine.SetPosition(0, ray.GetPoint(d));
                    isDrawing = true;
                    e.Use();
                    break;
                case EventType.MouseUp:
                    // Debug.Log("Stop drawing");
                    if (currentLine?.positionCount < 2)
                    {
                        DestroyImmediate(currentLine.gameObject);
                    }
                    currentLine = null;
                    isDrawing = false;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    plane.Raycast(ray, out d);
                    ++currentLine.positionCount;
                    currentLine.SetPosition(currentLine.positionCount - 1, ray.GetPoint(d));
                    e.Use();
                    break;
            }
        }
    }

    private void AddLine()
    {
        if (container == null)
        {
            container = new GameObject("Annotation")
            {
                // hideFlags = HideFlags.HideAndDontSave
            }.transform;
        }
        var go = new GameObject()
        {
            // hideFlags = HideFlags.HideAndDontSave
        };
        currentLine = go.AddComponent<LineRenderer>();
        currentLine.useWorldSpace = true;
        currentLine.widthMultiplier = ssWidth;
        currentLine.sharedMaterial = Resources.Load<Material>("M_Annotate");
        currentLine.startColor = currentLine.endColor = StrokeColor;
        currentLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        currentLine.numCapVertices = 3;
        // currentLine.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.1f, 1), new Keyframe(0.9f, 1), new Keyframe(1, 0));
        go.transform.SetParent(container);
        Undo.RegisterCreatedObjectUndo(go, "Annotation");
    }

    public void Clear()
    {
        DestroyImmediate(container.gameObject);
    }

    public override void OnActivated()
    {
        // base.OnActivated();
        Debug.Log("Start annotating");
        if (EditorWindow.GetWindow<SceneView>().TryGetOverlay("AnnotateOverlay", out var overlay))
        {
            overlay.displayed = true;
            (overlay as AnnotateOverlay).Init(this);
        }
        // if (container)
        // {
        //     DestroyImmediate(container.gameObject);
        // }
    }

    public override void OnWillBeDeactivated()
    {
        // base.OnWillBeDeactivated();
        Debug.Log("Stop annotating");
        if (EditorWindow.GetWindow<SceneView>().TryGetOverlay("AnnotateOverlay", out var overlay))
        {
            (overlay as AnnotateOverlay).Teardown();
            overlay.displayed = false;
        }
    }
}
