using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeckArtist.Tools
{
    [EditorTool("Annotate")]
    public class AnnotateTool : EditorTool
    {
        public float Smoothing;
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
        private Vector3 lastPoint;
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
            if (view == null)
            {
                return;
            }

            if (useCursor)
            {
                var cp = HandleUtility.WorldToGUIPoint(cursorPos);
                if (!view.position.Contains(cp + view.position.min))
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

            if (UnityEditor.Tools.viewToolActive && !e.shift) return;
            // if (e.modifiers.HasFlag(EventModifiers.Alt)) return;
            // if (!e.shift || e.alt) return;
            HandleUtility.AddDefaultControl(-1);

            ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            plane.Raycast(ray, out d);
            var p = ray.GetPoint(d);
            if (isDrawing)
            {
                DrawString(lastPoint, p, view.camera.transform.up);
            }
            if (e.isMouse)
            {
                switch (e.type)
                {
                    case EventType.MouseDown:
                        // ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                        if (e.button == 0)
                        {
                            isDrawing = true;
                            plane.Raycast(ray, out d);
                            lastPos = ray.GetPoint(d);
                            lastPoint = lastPos;
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
                            if (currentLine)
                            {
                                var n0 = currentLine.positionCount;
                                currentLine.Simplify(ssWidth * 0.02f);
                                var n1 = currentLine.positionCount;
                                var keys = new List<Keyframe>();
                                for (int i = 0; i < currentLine.widthCurve.length; i += n0 / n1)
                                {
                                    keys.Add(currentLine.widthCurve[i]);
                                }
                                keys.Add(currentLine.widthCurve[currentLine.widthCurve.length - 1]);
                                currentLine.widthCurve = new AnimationCurve(keys.ToArray());
                            }
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
                            var l = ssWidth;
                            l *= Mathf.Lerp(0, 10, Smoothing);
                            if ((p - lastPoint).sqrMagnitude > l * l)
                            {
                                if (currentLine == null)
                                {
                                    AddLine(lastPoint, view.camera.transform.rotation);
                                    currentLine.positionCount = 1;
                                    currentLine.SetPosition(0, currentLine.transform.InverseTransformPoint(lastPoint));
                                }
                                ++currentLine.positionCount;
                                lastPoint += (p - lastPoint).normalized * (p - lastPos).magnitude;
                                currentLine.SetPosition(currentLine.positionCount - 1, currentLine.transform.InverseTransformPoint(lastPoint));

                                var keys = new Keyframe[currentLine.widthCurve.length + 1];
                                for (int i = 0; i < currentLine.widthCurve.length; ++i)
                                {
                                    keys[i] = currentLine.widthCurve[i];
                                    keys[i].time *= currentLine.widthCurve.length / (currentLine.widthCurve.length + 1f);
                                }
                                var val = Pen.current.pressure.ReadValue();
                                keys[keys.Length - 1] = new Keyframe(1, val > 0 ? val : 1);
                                currentLine.widthCurve = new AnimationCurve(keys);
                                // TODO: Figure out why I need to create a new curve and assign it
                                // Modifying the keys of the existing curve does nothing (AddKey, key.time, etc)
                            }
                            lastPos = p;
                            // if (currentLine != null)
                            // {
                            //     currentLine.SetPosition(currentLine.positionCount - 1, p);
                            // }
                            e.Use();
                        }
                        break;
                }
            }
        }

        private void DrawString(Vector3 p0, Vector3 p1, Vector3 up)
        {
            var count = 10;
            var points = new Vector3[count];
            var l = 1 - (p1 - lastPoint).magnitude / (ssWidth * Mathf.Lerp(0, 10, Smoothing));
            for (int i = 0; i < count; ++i)
            {
                float t = (float)i / (count - 1);
                float tt = t * 2 - 1;
                points[i] = Vector3.Lerp(p0, p1, t) - up * (1 - tt * tt) * l * ssWidth * Mathf.Lerp(0, 10, Smoothing) / 2;
            }
            var c = Handles.color;
            Handles.color = Color.HSVToRGB(Mathf.Lerp(0, 120f / 360, 1 - l), 1, 1);
            Handles.DrawAAPolyLine(5, points);
            Handles.color = c;
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
                if (line == null) continue;
                line.widthMultiplier = HandleUtility.GetHandleSize(line.bounds.center) * Mathf.Lerp(0.01f, 0.1f, Width);
            }
        }

        private void AddLine(Vector3 pos, Quaternion rot)
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
            go.transform.SetPositionAndRotation(pos, rot);
            currentLine = go.AddComponent<LineRenderer>();
            // currentLine.useWorldSpace = true;
            currentLine.useWorldSpace = false;
            currentLine.widthMultiplier = ssWidth;
            currentLine.sharedMaterial = Resources.Load<Material>("M_Annotate");
            currentLine.startColor = currentLine.endColor = StrokeColor;
            currentLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            currentLine.numCapVertices = 3;
            // currentLine.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.1f, 1), new Keyframe(0.9f, 1), new Keyframe(1, 0));
            // currentLine.widthCurve = AnimationCurve.Constant(0, 1, 0);
            currentLine.widthCurve = new AnimationCurve();
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