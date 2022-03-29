using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

[EditorTool("Annotate")]
public class AnnotateTool : EditorTool
{
    public float Width = 0.5f;
    private float lastWidth;
    private float ssWidth;
    public Color StrokeColor = Color.cyan * 0.7f;
    public Color PlaneColor = Color.cyan * 0.25f;
    private Transform container;
    private LineRenderer currentLine;
    private LineRenderer[] lines;
    private bool isDrawing;
    private Plane plane;
    private Ray ray;
    private float d;
    private Vector3 lastPos;
    public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("editicon.sml");

    [Shortcut("Annotations/Annotate", typeof(SceneView), KeyCode.N)]
    static void ToggleAnnotate(ShortcutArguments args)
    {
        ToolManager.SetActiveTool<AnnotateTool>();
    }

    public override void OnToolGUI(EditorWindow window)
    {
        // base.OnToolGUI(window);
        Event e = Event.current;
        UpdateLineWidth();
        // if (e.modifiers.HasFlag(EventModifiers.Alt)) return;
        if (!e.shift || e.alt) return;

        var view = window as SceneView;
        HandleUtility.AddDefaultControl(-1);

        plane.SetNormalAndPosition(-view.camera.transform.forward, view.pivot);
        ssWidth = HandleUtility.GetHandleSize(view.pivot) * Mathf.Lerp(0.01f, 0.1f, Width);

        ray = HandleUtility.GUIPointToWorldRay(Vector2.zero);
        plane.Raycast(ray, out d);

        var color = Handles.color;
        var zTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        Handles.color = PlaneColor;
        var v = ray.GetPoint(d) - view.pivot;
        Handles.DrawSolidDisc(view.pivot, -view.camera.transform.forward, v.magnitude);

        Handles.color = color;
        Handles.zTest = zTest;
        if (e.isMouse && e.button == 0)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    isDrawing = true;
                    ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                    plane.Raycast(ray, out d);
                    lastPos = ray.GetPoint(d);
                    e.Use();
                    break;
                case EventType.MouseUp:
                    isDrawing = false;
                    currentLine = null;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (isDrawing)
                    {
                        ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        plane.Raycast(ray, out d);
                        var p = ray.GetPoint(d);
                        if ((p - lastPos).sqrMagnitude > (ssWidth / 2) * (ssWidth / 2))
                        {
                            if (currentLine == null)
                            {
                                AddLine();
                                currentLine.positionCount = 1;
                                currentLine.SetPosition(0, lastPos);
                            }
                            ++currentLine.positionCount;
                            lastPos = p;
                        }
                        if (currentLine != null)
                        {
                            currentLine.SetPosition(currentLine.positionCount - 1, p);
                        }
                    }
                    e.Use();
                    break;
            }
        }
    }

    public void UpdateLineWidth()
    {
        if (container == null)
        {
            return;
        }
        foreach (var line in lines)
        {
            line.widthMultiplier = HandleUtility.GetHandleSize(line.bounds.center) * Mathf.Lerp(0.01f, 0.1f, Width);
        }
    }

    private void AddLine()
    {
        if (container == null)
        {
            container = new GameObject("Annotation")
            {
                hideFlags = HideFlags.HideAndDontSave
            }.transform;
        }
        var go = new GameObject()
        {
            hideFlags = HideFlags.HideAndDontSave
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
        lines = container.GetComponentsInChildren<LineRenderer>();
    }

    public void Clear()
    {
        if (container == null)
        {
            return;
        }
        Undo.DestroyObjectImmediate(container.gameObject);
        Undo.SetCurrentGroupName("Clear Annotations");
    }

    private void OnUndoRedo()
    {
        lines = container?.GetComponentsInChildren<LineRenderer>();
    }

    public override void OnActivated()
    {
        // base.OnActivated();
        // Debug.Log("Start annotating");
        if (EditorWindow.GetWindow<SceneView>().TryGetOverlay("AnnotateOverlay", out var overlay))
        {
            overlay.displayed = true;
            (overlay as AnnotateOverlay).Init(this);
        }
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    public override void OnWillBeDeactivated()
    {
        // base.OnWillBeDeactivated();
        // Debug.Log("Stop annotating");
        if (EditorWindow.GetWindow<SceneView>().TryGetOverlay("AnnotateOverlay", out var overlay))
        {
            (overlay as AnnotateOverlay).Teardown();
            overlay.displayed = false;
        }
        Undo.undoRedoPerformed -= OnUndoRedo;
    }
}
