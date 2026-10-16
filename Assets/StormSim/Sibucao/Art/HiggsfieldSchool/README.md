# Philippine school classroom asset

Created for StormSim through Higgsfield 3D Jutsu, project `871075c7-cafe-4a75-8c5d-21e695e56ff5`, committed revision 1, operation `school-build-02`.

The GLB preserves the editable semantic parts and portable PBR materials. The Unity prefab batches those parts by material and adds simple architectural collision, without importing the delivery camera or lights. Two instances replace the plain classroom blocks beside the covered court in SIBUCAO_RAIN. The original wall and roof objects remain in the scene, inactive, for rollback.

Design: shaded veranda, raised concrete slab and steps, jalousie-style window blades with security grilles, green dado and doors, pitched ribbed metal roof, rain downpipes, school and room signs, benches and noticeboard. This is a stylized Philippine public-school asset, not a measured reconstruction of the actual school. Classroom doors are closed; no classroom-interior gameplay was added.

Evacuation arrival, dry-refuge protection, flood simulation, route and decision systems are unchanged. Installation is scoped to school art; the campus layout, court, flagpole and surrounding buildings are retained.

Verified the Higgsfield preview and rainy Unity Play-mode appearance. The installed pair contains 20 renderers, 66 simple architectural colliders, 19,260 triangles, no imported cameras/lights and no unsupported materials. A capsule sampling check along the arrival-to-court corridor found zero new school collisions. The refuge component's serialized settings matched before and after installation. This does not constitute a complete evacuation playthrough.

Source project: https://higgsfield.ai/3d-jutsu/871075c7-cafe-4a75-8c5d-21e695e56ff5
