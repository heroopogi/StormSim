# StormSim local compatibility fix

This is an embedded copy of Meta XR Core SDK 205.0.0. Unity uses this copy
instead of the downloaded package, keeping the fix across Library rebuilds.

`Editor/RuntimeOptimizer/PerformanceInsight/CaptureTool.cs` resolves the Android
SDK through Unity editor preferences, the embedded SDK location, and the
ANDROID_SDK_ROOT environment variable. It no longer directly references
AndroidExternalToolsSettings, which is unavailable when the Android Build
Support module is not installed. A missing SDK resolves to an empty path,
which the existing OVRADBTool handles as not ready.

`Editor/UnityADBToolSingleton.cs` also disables the missing-SDK dialog for
background device checks during editor startup. Explicit Android build checks
retain their existing diagnostics.

Both SDK lookups now check BuildPipeline.IsBuildTargetSupported before asking
for the Android playback directory, avoiding the "Build target directory for
AndroidPlayer does not exist" startup error on Windows-only installations.
The updated Oculus.VR.Editor and Meta.XR.RuntimeOptimizer.Editor assemblies
both pass compilation with the project's Unity compiler and references.

Validation: the complete Meta.XR.RuntimeOptimizer.Editor assembly compiled
successfully with Unity 6000.5.7f1's C# compiler and the project's existing
compiler arguments and references, using the embedded package sources.
This does not constitute a player build or a headset capture test.

When upgrading Meta XR, update this embedded package as well and check whether
the upstream CaptureTool still needs this compatibility fix.
