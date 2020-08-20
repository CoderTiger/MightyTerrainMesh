
public interface IMTLoaderObserver
{
    void BeforeLoadMesh(int meshId, int lod);
    void AfterLoadMesh(int meshId, int lod);
    void BeforeUnloadMesh(int meshId, int lod);
    void AfterUnloadMesh(int meshId, int lod);
}
