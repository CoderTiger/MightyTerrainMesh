using UnityEngine;

public interface IMTLoaderObserver
{
    void OnMeshUpdated(GameObject owner, int meshId, int lod);
}
