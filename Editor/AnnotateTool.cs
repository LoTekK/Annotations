using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace TeckArtist.Tools
{
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
        private Vector2 clickPos;
        private Vector3 cursorPos;
        private bool useCursor;
        public override GUIContent toolbarIcon => EditorGUIUtility.IconContent("editicon.sml");

        [Shortcut("Annotations/Annotate", typeof(SceneView), KeyCode.D)]
        static void ToggleAnnotate(ShortcutArguments args)
        {
            ToolManager.SetActiveTool<AnnotateTool>();
        }

        public override void OnToolGUI(EditorWindow window)
        {
            // base.OnToolGUI(window);
            Event e = Event.current;
            UpdateLineWidth();

            var view = window as SceneView;

            if (useCursor)
            {
                var p = HandleUtility.WorldToGUIPoint(cursorPos);
                if (!view.position.Contains(p + view.position.min))
                {
                    useCursor = false;
                }
            }

            plane.SetNormalAndPosition(-view.camera.transform.forward, useCursor ? cursorPos : view.pivot);
            ssWidth = HandleUtility.GetHandleSize(view.pivot) * Mathf.Lerp(0.01f, 0.1f, Width);

            var color = Handles.color;
            var zTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.color = PlaneColor;

            ray = HandleUtility.GUIPointToWorldRay(Vector2.zero);
            plane.Raycast(ray, out d);
            var p0 = ray.GetPoint(d);
            ray = HandleUtility.GUIPointToWorldRay(window.position.size);
            plane.Raycast(ray, out d);
            var p1 = ray.GetPoint(d);
            var v = p1 - p0;
            Handles.DrawSolidDisc(useCursor ? cursorPos : view.pivot, -view.camera.transform.forward, v.magnitude);

            // Handles.color = Color.cyan * 0.5f;
            Handles.color = Color.yellow;
            Handles.SphereHandleCap(-1, useCursor ? cursorPos : view.pivot, Quaternion.identity, ssWidth * 2, EventType.Repaint);
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = Color.white;
            Handles.DrawWireDisc(useCursor ? cursorPos : view.pivot, -view.camera.transform.forward, ssWidth * 4);
            Handles.color = Color.red;
            Handles.DrawWireDisc(useCursor ? cursorPos : view.pivot, -view.camera.transform.forward, ssWidth * 3);
            // Handles.DrawSolidDisc(useCursor ? cursorPos : view.pivot, -view.camera.transform.forward, ssWidth / 2);

            Handles.color = color;
            Handles.zTest = zTest;

            if (e.modifiers.HasFlag(EventModifiers.Alt)) return;
            // if (!e.shift || e.alt) return;
            HandleUtility.AddDefaultControl(-1);

            if (e.isMouse)
            {
                switch (e.type)
                {
                    case EventType.MouseDown:
                        ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        if (e.button == 0)
                        {
                            isDrawing = true;
                            plane.Raycast(ray, out d);
                            lastPos = ray.GetPoint(d);
                            clickPos = e.mousePosition;
                            e.Use();
                        }
                        if (e.button == 1 && e.shift)
                        {
                            var go = HandleUtility.PickGameObject(e.mousePosition, false);
                            if (go && go.TryGetComponent<MeshFilter>(out var mf))
                            {
                                if (MeshRaycast.IntersectRayMesh(ray, mf, out var hit))
                                {
                                    // view.pivot = hit.point;
                                    cursorPos = hit.point;
                                    useCursor = true;
                                }
                            }
                            e.Use();
                        }
                        break;
                    case EventType.MouseUp:
                        if (e.button == 0)
                        {
                            isDrawing = false;
                            currentLine = null;
                            if ((e.mousePosition - clickPos).sqrMagnitude < 4)
                            {
                                Selection.objects = new UnityEngine.Object[] { HandleUtility.PickGameObject(e.mousePosition, false) };
                            }
                            e.Use();
                        }
                        break;
                    case EventType.MouseDrag:
                        if (isDrawing)
                        {
                            ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                            plane.Raycast(ray, out d);
                            var p = ray.GetPoint(d);
                            var l = ssWidth / 2;
                            // l *= smoothing;
                            if ((p - lastPos).sqrMagnitude > l * l)
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
                            e.Use();
                        }
                        break;
                }
            }
        }

        public void ClearCursor()
        {
            cursorPos = Vector3.zero;
            useCursor = false;
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

    static class MeshRaycast
    {
        static Type type_HandleUtility;
        static MethodInfo meth_IntersectRayMesh;
        static bool init;

        static MeshRaycast()
        {
            if (init)
            {
                return;
            }
            var editorTypes = typeof(Editor).Assembly.GetTypes();

            type_HandleUtility = editorTypes.FirstOrDefault(t => t.Name == "HandleUtility");
            meth_IntersectRayMesh = type_HandleUtility.GetMethod("IntersectRayMesh",
                                                                  BindingFlags.Static | BindingFlags.NonPublic);
            init = true;
        }

        //get a point from interected with any meshes in scene, based on mouse position.
        //WE DON'T NOT NEED to have to have colliders ;)
        //usually used in conjunction with  PickGameObject()
        public static bool IntersectRayMesh(Ray ray, MeshFilter meshFilter, out RaycastHit hit)
        {
            return IntersectRayMesh(ray, meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, out hit);
        }

        //get a point from interected with any meshes in scene, based on mouse position.
        //WE DON'T NOT NEED to have to have colliders ;)
        //usually used in conjunction with  PickGameObject()
        public static bool IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit)
        {
            var parameters = new object[] { ray, mesh, matrix, null };
            bool result = (bool)meth_IntersectRayMesh.Invoke(null, parameters);
            hit = (RaycastHit)parameters[3];
            return result;
        }
    }
}