using UnityEngine;

public interface IMTLoaderObserver
{
    void OnMeshLoaded(GameObject owner, int meshId, int lod);
    void OnMeshUnloading(GameObject owner, int meshId, int lod);
}
