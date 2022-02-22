# Magrathea

Build worlds in Unity3D and deploy them to an archipelago of open metaverse platforms.

## Getting Started
Export any Unity scene as a GLB with menu item Magrathea->Export Scene.

This will bring up an export configuration window. 

### Supported Components
**Lights**: point and direction light are currently supported.

**Cameras**: 

**Lightmaps**: 

  ***BAKE_COMBINED***: lightmaps are automatically combined with the diffuse channel and reprojected onto the mesh's uv0, then exported as an unlit material. Note that this will cause issues with instanced geometry.

  ***BAKE_SEPARATE***: lightmaps are exported as-is and loaded into the lightmap in the standard mesh material. Mesh uv2s are adjusted to apply lightmap scale and offset. 

**Colliders**: box and mesh colliders are automatically configured and exported.

**LODs**: LOD Groups in Unity are automatically configured and exported. 

**Spawn Points**: Spawn points are exported by adding the ***Spawn Point*** script onto transforms in the scene. 

**Instancing**: Any Gameobjects which share the same mesh and material will be instanced by default. Currently only meshes with one material are supported. As previously noted, baking lightmaps onto Gameobjects that share the same mesh and material will break instancing.

**Skybox**: If the scene has a skybox with a valid cubemap, then it is exported into the scene. Currently only supports one cubemap per scene.