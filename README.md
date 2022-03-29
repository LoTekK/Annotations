# TeckArtist Annotations
Tool to draw or write annotations in the scene view
---
# Overview
## Usage
Select `Annotations` from the Custom Tool dropdown in the SceneView Toolbar. When in this mode, an overlay shows up to allow you to change options such as stroke width, stroke colour, and the color of the drawing plane. Scene view navigation (eg. `Alt-LMB` to orbit or `Alt-RMB` to zoom) is still available.

To place a stroke, hold shift; the drawing plane will be displayed, and LMB dragging will draw a stroke. `Undo` and `Redo` are supported. The `Clear` button on the overlay panel will clear all strokes.

Currently the stroke container has its `hideFlags` set to `HideAndDontSave`, so the strokes won't contaminate your scenes.
---
# Known Issues
---
# Todo
Possibly looking into how best to handle persistent annotations that can be checked into source control without contaminating the scene.