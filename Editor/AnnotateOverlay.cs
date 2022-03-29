using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[Overlay(typeof(SceneView), "AnnotateOverlay", "Annotate")]
public class AnnotateOverlay : Overlay
{
    private AnnotateTool tool;
    private VisualElement m_Panel;
    private bool m_Drawing;

    public override VisualElement CreatePanelContent()
    {
        UpdatePanel();
        return m_Panel;
    }

    private void UpdatePanel()
    {
        if (m_Panel == null)
        {
            m_Panel = new VisualElement();
        }
    }

    public void Init(AnnotateTool _tool)
    {
        UpdatePanel();
        tool = _tool;
        var so = new SerializedObject(tool);
        var width = new Slider("Width")
        {
            // value = tool.Width,
            lowValue = 0,
            highValue = 1,
            showInputField = true
        };
        width.BindProperty(so.FindProperty(nameof(tool.Width)));
        m_Panel.Add(width);
        var strokeColor = new ColorField("Stroke Color");
        strokeColor.BindProperty(so.FindProperty(nameof(tool.StrokeColor)));
        m_Panel.Add(strokeColor);
        var planeColor = new ColorField("Plane Color");
        planeColor.BindProperty(so.FindProperty(nameof(tool.PlaneColor)));
        m_Panel.Add(planeColor);
        var button = new Button(() => tool.Clear())
        {
            text = "Clear"
        };
        m_Panel.Add(button);
        m_Panel.MarkDirtyRepaint();
    }

    public void Teardown()
    {
        UpdatePanel();
        tool = null;
        m_Panel.Clear();
        m_Panel.MarkDirtyRepaint();
    }
}
