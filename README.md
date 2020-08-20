# Intro
Forked the cool code from [jinsek/MightyTerrainMesh](https://github.com/jinsek/MightyTerrainMesh)

## Fixed two issues below,
1. Cannot build iOS package due to using editor code.
2. Cannot load mesh on iOS device because not using Resources.Load.

## Other changes,
1. Add mesh collider on loading mesh (configurable in inspector).
2. Set layer as "Ground" on loading mesh (configurable in inspector).
3. Name mesh gameobject with its ID as the tail on creating preview.
4. Introduced LoadMode (as below) to load terrain data by strategy.
   1. SpeedOptimized: load meshes of all levels of detail when active.
   2. MemoryOptimized: only load mesh of the active level of detail, and load the other resources only if necessary.
5. Added observer interface for MTLoader to provide chances to loading external associated terrain mesh resources such as heightmaps.

# MightyTerrainMesh
A Unity Plugin for Converting Terrain 2 Mesh with LOD & QaudTree infomation.

There are some introduction & result here :

https://zhuanlan.zhihu.com/p/64809281

And thanks for *parahunter*, I used his [triangle-net-for-unity](https://github.com/parahunter/triangle-net-for-unity) as the tessellation solution.
