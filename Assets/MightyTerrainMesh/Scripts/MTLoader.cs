using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MightyTerrainMesh;

internal class MTPatch
{
    private bool _addMeshCollider = false;
    private int _meshLayer = 0;
    private static Queue<MTPatch> _qPool = new Queue<MTPatch>();
#if DEBUG
    public static MTPatch Pop(Material[] mats, bool addMeshCollider, int meshLayer, int meshId, int lod)
#else// !DEBUG
    public static MTPatch Pop(Material[] mats, bool addMeshCollider, int meshLayer)
#endif// DEBUG
    {
        if (_qPool.Count > 0)
        {
            return _qPool.Dequeue();
        }
#if DEBUG
        return new MTPatch(mats, addMeshCollider, meshLayer, meshId, lod);
#else// !DEBUG
        return new MTPatch(mats, addMeshCollider, meshLayer);
#endif
    }
    public static void Push(MTPatch p)
    {
        p.mGo.SetActive(false);
        _qPool.Enqueue(p);
    }
    public static void Clear()
    {
        while(_qPool.Count > 0)
        {
            _qPool.Dequeue().DestroySelf();
        }
    }
    public uint PatchId { get; private set; }
    private GameObject mGo;
    private MeshFilter mMesh;
    private MeshCollider mCollider;
#if DEBUG
    public MTPatch(Material[] mats, bool addMeshCollider, int meshLayer, int meshId, int lod)
#else// !DEBUG
    public MTPatch(Material[] mats, bool addMeshCollider, int meshLayer)
#endif// DEBUG
    {
        _addMeshCollider = addMeshCollider;
        _meshLayer = meshLayer;
#if DEBUG
        mGo = new GameObject(string.Format("_mtpatch_{0}_{1}", meshId, lod));
#else// !DEBUG
        mGo = new GameObject("_mtpatch");
#endif// DEBUG
        MeshRenderer meshR;
        mMesh = mGo.AddComponent<MeshFilter>();
        meshR = mGo.AddComponent<MeshRenderer>();
        meshR.materials = mats;
        // added by Coder Tiger
        if (_addMeshCollider)
        {
            mCollider = mGo.AddComponent<MeshCollider>();
        }
        mGo.layer = _meshLayer;
    }
    public void Reset(uint id, Mesh m)
    {
        mGo.SetActive(true);
        PatchId = id;
        mMesh.mesh = m;
        if (_addMeshCollider)
        {
            GameObject.Destroy(mCollider);
            mCollider = mGo.AddComponent<MeshCollider>();
        }
    }
    private void DestroySelf()
    {
        if (mGo != null)
            MonoBehaviour.Destroy(mGo);
        mGo = null;
        mMesh = null;
        mCollider = null;
    }
}
internal class MTRuntimeMesh
{
    public int MeshID { get; private set; }
    private Mesh[] mLOD;
    private MTLoader.LoadMode _loadMode = MTLoader.LoadMode.SpeedOptimized;
    public MTRuntimeMesh(int meshid, int lod, string dataName)
    {
        MeshID = meshid;
        mLOD = new Mesh[lod];
        _loadMode = MTLoader.LoadMode.SpeedOptimized;
        MTFileUtils.LoadMesh(mLOD, dataName, meshid);
    }
    public MTRuntimeMesh(uint patchId, string dataName)
    {
        //MeshID = (int)(patchId >> 2);
        (int meshID, int lod) = MTLoader.SplitPatchId(patchId);
        MeshID = meshID;
        mLOD = new Mesh[1];
        _loadMode = MTLoader.LoadMode.MemoryOptimized;
        MTFileUtils.LoadMesh(mLOD, dataName, MeshID, lod);
    }
    public Mesh GetMesh(int lod)
    {
        if (_loadMode == MTLoader.LoadMode.SpeedOptimized)
        {
            lod = Mathf.Clamp(lod, 0, mLOD.Length - 1);
            return mLOD[lod];
        }
        return mLOD[0];
    }
}

public class MTLoader : MonoBehaviour
{
    public enum LoadMode
    {
        SpeedOptimized,
        MemoryOptimized
    }

    public string DataName = "";
    [Header("LOD distance")]
    public float[] lodPolicy = new float[1] { 0 };
    [Header("Advanced")]
    public bool addMeshCollider = false;
    [SerializeField, Layer] public int meshLayer = 0;
    public LoadMode loadMode = LoadMode.SpeedOptimized;

    private Camera mCamera;
    private MTQuadTreeHeader mHeader;
    private MTQuadTreeNode mRoot;
    //patch identifier [meshid 30bit][lod 2bit]
    private MTArray<uint> mVisiblePatches;
    private Dictionary<uint, MTPatch> mActivePatches = new Dictionary<uint, MTPatch>();
    private Dictionary<uint, MTPatch> mPatchesFlipBuffer = new Dictionary<uint, MTPatch>();
    private Dictionary<uint, MTRuntimeMesh> mActiveMeshes = new Dictionary<uint, MTRuntimeMesh>();
    //meshes
    private Dictionary<int, MTRuntimeMesh> mMeshPool = new Dictionary<int, MTRuntimeMesh>();
    private bool mbDirty = true;

    private IMTLoaderObserver _observer;

    static public (int, int) SplitPatchId(uint patchId)
    {
        int mId = (int)(patchId >> 2);
        int lod = (int)(patchId & 0x00000003);
        return (mId, lod);
    }
    private Mesh GetMesh(uint patchId)
    {
        if (loadMode == LoadMode.SpeedOptimized)
        {
            (int mId, int lod) = SplitPatchId(patchId);
            if (mMeshPool.ContainsKey(mId))
            {
                return mMeshPool[mId].GetMesh(lod);
            }
            if (_observer != null)
            {
                for (int i = 0; i < mHeader.LOD; i++)
                {
                    _observer.BeforeLoadMesh(mId, i);
                }
            }
            MTRuntimeMesh rm = new MTRuntimeMesh(mId, mHeader.LOD, mHeader.DataName);
            if (_observer != null)
            {
                for (int i = 0; i < mHeader.LOD; i++)
                {
                    _observer.AfterLoadMesh(mId, i);
                }
            }
            mMeshPool.Add(mId, rm);
            return rm.GetMesh(lod);
        }
        return GetActiveMesh(patchId);
    }
    private Mesh GetActiveMesh(uint patchId)
    {
        Debug.Assert(loadMode == LoadMode.MemoryOptimized, string.Format("loadMode SHOULD be LoadMode.MemoryOptimized but {0}", loadMode));
        if (mActiveMeshes.ContainsKey(patchId))
        {
            return mActiveMeshes[patchId].GetMesh(0);
        }
        (int meshId, int lod) = SplitPatchId(patchId);
        if (_observer != null)
            _observer.BeforeLoadMesh(meshId, lod);
        MTRuntimeMesh rm = new MTRuntimeMesh(patchId, mHeader.DataName);
        if (_observer != null)
            _observer.AfterLoadMesh(meshId, lod);
        mActiveMeshes.Add(patchId, rm);
        return rm.GetMesh(0);
    }
    public void SetDirty()
    {
        mbDirty = true;
    }
    private void Awake()
    {
        if (DataName == "")
            return;
        try
        {
            mHeader = MTFileUtils.LoadQuadTreeHeader(DataName);
            mRoot = new MTQuadTreeNode(mHeader.QuadTreeDepth, mHeader.BoundMin, mHeader.BoundMax);
            foreach (var mh in mHeader.Meshes.Values)
                mRoot.AddMesh(mh);
            int gridMax = 1 << mHeader.QuadTreeDepth;
            mVisiblePatches = new MTArray<uint>(gridMax * gridMax);
            if (lodPolicy.Length < mHeader.LOD)
            {
                float[] policy = new float[mHeader.LOD];
                for (int i = 0; i < lodPolicy.Length; ++i)
                    policy[i] = lodPolicy[i];
                lodPolicy = policy;
            }
            lodPolicy[0] = Mathf.Clamp(lodPolicy[0], 0.5f * mRoot.Bound.size.x / gridMax, lodPolicy[0]);
            lodPolicy[lodPolicy.Length - 1] = float.MaxValue;
        }
        catch
        {
            mHeader = null;
            mRoot = null;
            MTLog.LogError("MTLoader load quadtree header failed");
        }
        mCamera = GetComponent<Camera>();
    }
    private void OnDestroy()
    {
        MTPatch.Clear();
    }
    // Start is called before the first frame update
    void Start()
    {
        _observer = GetComponent<IMTLoaderObserver>();
    }

    // Update is called once per frame
    void Update()
    {
        //every 10 frame update once
        if (mCamera == null || mRoot == null || !mCamera.enabled || !mbDirty)
            return;
        mbDirty = false;
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mCamera);
        mVisiblePatches.Reset();
        mRoot.RetrieveVisibleMesh(planes, transform.position, lodPolicy,  mVisiblePatches);
        mPatchesFlipBuffer.Clear();
        for (int i = 0; i < mVisiblePatches.Length; ++i)
        {
            uint pId = mVisiblePatches.Data[i];
            if (mActivePatches.ContainsKey(pId))
            {
                mPatchesFlipBuffer.Add(pId, mActivePatches[pId]);
                mActivePatches.Remove(pId);
            }
            else
            {
                //new patches
                Mesh m = GetMesh(pId);
                if (m != null)
                {
#if DEBUG
                    (int meshId, int lod) = SplitPatchId(pId);
                    MTPatch patch = MTPatch.Pop(mHeader.RuntimeMats, addMeshCollider, meshLayer, meshId, lod);
#else// !DEBUG
                    MTPatch patch = MTPatch.Pop(mHeader.RuntimeMats, addMeshCollider, meshLayer);
#endif// DEBUG
                    patch.Reset(pId, m);
                    mPatchesFlipBuffer.Add(pId, patch);
                }
            }
        }
        Dictionary<uint, MTPatch>.Enumerator iPatch = mActivePatches.GetEnumerator();
        Queue<uint> inactivePatcheIDs = new Queue<uint>();
        if (_observer != null)
        {
            while (iPatch.MoveNext())
            {
                (int meshID, int lod) = SplitPatchId(iPatch.Current.Key);
                inactivePatcheIDs.Enqueue(iPatch.Current.Key);
                _observer.BeforeUnloadMesh(meshID, lod);
                MTPatch.Push(iPatch.Current.Value);
            }
        }
        else
        {
            while (iPatch.MoveNext())
            {
                MTPatch.Push(iPatch.Current.Value);
            }
        }
        mActivePatches.Clear();
        if (_observer != null)
        {
            foreach (uint patchID in inactivePatcheIDs)
            {
                (int meshID, int lod) = SplitPatchId(patchID);
                _observer.AfterUnloadMesh(meshID, lod);
            }
            inactivePatcheIDs.Clear();
        }
        Dictionary<uint, MTPatch> temp = mPatchesFlipBuffer;
        mPatchesFlipBuffer = mActivePatches;
        mActivePatches = temp;
    }
}
