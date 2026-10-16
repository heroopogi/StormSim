# StormSim Camera

The `StormSim Camera` in `MAIN_GAME` uses a normal Unity Camera and a local-space controller with Unity's Input System. The controller has no Cesium dependencies. Cesium terrain still streams through the existing camera manager's **Use Main Camera** setting. The scene's fixed georeference is retained for its local terrain and physics.

Click the Game view in Play mode to control the camera:

| Control | Action |
| --- | --- |
| WASD / arrow keys | Fly forward, backward, left and right |
| Q / E | Move down / up along the local vertical axis |
| Hold right mouse + drag | Look around |
| Hold middle mouse + drag | Pan across the view |
| Alt + hold left mouse + drag | Orbit the clicked surface (uses a ground plane or forward point if no collider is hit) |
| Mouse wheel | Move closer / farther; change radius while orbiting |
| Shift / Ctrl | Faster / slower movement and zoom |
| Home | Restore the starting view |
| Escape | Release the cursor and stop motion |

The Inspector exposes speed, smoothing, sensitivity, zoom distance, and optional movement bounds in the camera parent's coordinate space. Bounds start disabled so the existing study area remains accessible. This is a free-flight survey camera; it can pass through geometry and does not include a walking/ground-collision mode. Its starting view is captured when the component first enables.

Mouse gestures do not start over UI. Selected UI controls keep keyboard focus; right-click the view to resume navigation. Releasing the drag, disabling the controller or leaving the app releases its cursor capture. Input uses unscaled time so the camera can move while simulation time is paused.

Installation retains the original Camera, Transform, AudioListener and URP component, preserving their serialized references. Cesium's movement, fly-to, origin-shift and globe-anchor components are removed from the camera. Near/far clipping is 0.1 / 20,000 local units. Do not enable origin shifting elsewhere without adapting the local terrain, physics and camera together.

`Tools > StormSim > Camera > Install Custom Camera` can install this setup on another saved scene's MainCamera. It backs up both the saved and current scene to `RoadRepairBackup/CustomCamera_<timestamp>` and supports Unity Undo.
