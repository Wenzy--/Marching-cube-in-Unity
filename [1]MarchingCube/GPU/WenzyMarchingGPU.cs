using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WenzyMarchingGPU : MonoBehaviour
{
    public ComputeShader marchingCS;
    int kernel;
    int maxTriNum;

    public struct Triangles
    {
        public Vector3 posA, posB, posC;
        public Vector3 normalA, normalB, normalC;
    }

    RenderTexture VoxelTexture;
    public ComputeBuffer Tribuffer;
    public ComputeBuffer PosBuffer,NormalBuffer,IndexBuffer;
    public ComputeBuffer CountBuffer; 

    Mesh mesh;
    [Header("Marching cube feature")]
    public Vector3 CenterPos;
    public bool EnableSmooth;
    public Material mat;
    public int GridRes = 8; // usually the gridres must be the multiples of 8
    public float GridW = 2f;
    [Header("Animation")]
    public float NoiseInterval = 0.05f;
    public float IsoLevel = 0.5f;

    void Start()
    {
        VoxelTexture = new RenderTexture(GridRes, GridRes, 0,RenderTextureFormat.RFloat);
        VoxelTexture.enableRandomWrite = true;
        VoxelTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
        VoxelTexture.volumeDepth = GridRes;
        VoxelTexture.filterMode = FilterMode.Point;
        VoxelTexture.wrapMode = TextureWrapMode.Repeat;
        VoxelTexture.useMipMap = false;
        VoxelTexture.Create();

        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>().material = mat;
        mesh = GetComponent<MeshFilter>().mesh;
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;


        maxTriNum = GridRes * GridRes * GridRes * 5; // the max triangle num

        int maxValue = maxTriNum;
        Tribuffer = new ComputeBuffer(maxValue, sizeof(float) * 3 * 3 * 2, ComputeBufferType.Append);
        PosBuffer = new ComputeBuffer(maxValue * 3, sizeof(float) * 3 * 3, ComputeBufferType.Default);
        NormalBuffer = new ComputeBuffer(maxValue * 3, sizeof(float) * 3 * 3, ComputeBufferType.Default);
        IndexBuffer = new ComputeBuffer(maxValue * 3, sizeof(int) * 3, ComputeBufferType.Default);
        CountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    void UpdateMesh()
    {
        Tribuffer.SetCounterValue(0);

        // common
        marchingCS.SetInt("GridRes", GridRes);
        marchingCS.SetFloat("Time", Time.time);
        marchingCS.SetFloat("NoiseInterval", NoiseInterval);
        marchingCS.SetFloat("IsoLevel", IsoLevel);
        marchingCS.SetBool("EnableSmooth", EnableSmooth);
        marchingCS.SetVector("CenterPos", CenterPos);
        marchingCS.SetFloat("GridW", GridW);

        kernel = marchingCS.FindKernel("UpdateVoxel");
        marchingCS.SetTexture(kernel, "VoxelTexture", VoxelTexture);
        marchingCS.Dispatch(kernel, GridRes, GridRes, GridRes);

        kernel = marchingCS.FindKernel("CalculateTriangle");
        marchingCS.SetTexture(kernel, "VoxelTexture", VoxelTexture);
        marchingCS.SetBuffer(kernel, "Tribuffer", Tribuffer);
        marchingCS.Dispatch(kernel, GridRes/8, GridRes/8, GridRes/8);
        
        ComputeBuffer.CopyCount(Tribuffer, CountBuffer, 0);
        int[] countArr = { 0 };
        CountBuffer.GetData(countArr);
        int count = countArr[0]; // the actual num

        if (count != 0)
        {
            kernel = marchingCS.FindKernel("ExtractData");
            marchingCS.SetInt("TriangleCount", count);
            marchingCS.SetBuffer(kernel, "NewTriBuffer", Tribuffer);
            marchingCS.SetBuffer(kernel, "PosBuffer", PosBuffer);
            marchingCS.SetBuffer(kernel, "NormalBuffer", NormalBuffer);
            marchingCS.SetBuffer(kernel, "IndexBuffer", IndexBuffer);
            marchingCS.Dispatch(kernel, (int)Mathf.Ceil(count / 10f), 1, 1);
        }

        Vector3[] posArr = new Vector3[count * 3];
        PosBuffer.GetData(posArr, 0, 0, count * 3);
        Vector3[] normalArr = new Vector3[count * 3];
        NormalBuffer.GetData(normalArr, 0, 0, count * 3);

       
        int[] indexArr = new int[count * 3];
        IndexBuffer.GetData(indexArr, 0, 0, count * 3);

        mesh.Clear();
        mesh.vertices = posArr;
        mesh.normals = normalArr;
        mesh.triangles = indexArr;
    }

    void Update()
    {
        UpdateMesh();
    }

    private void OnDisable()
    {
        Tribuffer.Release();
        Tribuffer.Dispose();
        PosBuffer.Release();
        PosBuffer.Dispose();
        NormalBuffer.Release();
        NormalBuffer.Dispose();
        IndexBuffer.Release();
        IndexBuffer.Dispose();
        CountBuffer.Release();
        CountBuffer.Dispose();
    }
}
