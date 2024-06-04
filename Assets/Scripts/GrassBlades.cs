﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;

public class GrassBlades : MonoBehaviour
{
    struct GrassBlade
    {
        public Vector3 position;
        public float bend; // 随机草叶倾倒
        public float noise;// CS计算噪声值
        public float fade; // 随机草叶明暗
        public float face; // 叶片朝向

        public GrassBlade( Vector3 pos)
        {
            position.x = pos.x;
            position.y = pos.y;
            position.z = pos.z;
            bend = 0;
            noise = Random.Range(0.5f, 1) * 2 - 1;
            fade = Random.Range(0.99f, 1);
            face = Random.Range(0, 1);
        }
    }
    int SIZE_GRASS_BLADE = 7 * sizeof(float); // adding face

    [Range(0,0.5f)]
    public float width = 0.2f;
    [Range(0,2)]
    public float height = 1f;
    public Material material;
    public ComputeShader shader;
    public Material visualizeNoise;
    public bool viewNoise = false;
    [Range(0,1)]
    public float density;
    [Range(0.1f,3)]
    public float scale;
    [Range(10, 45)]
    public float maxBend;
    [Range(0, 20)]
    public float windSpeed;
    [Range(0, 360)]
    public float windDirection;
    [Range(10, 1000)]
    public float windScale;

    ComputeBuffer bladesBuffer;
    ComputeBuffer argsBuffer;
    GrassBlade[] bladesArray;
    uint[] argsArray = new uint[] { 0, 0, 0, 0, 0 };
    Bounds bounds;
    int timeID;
    int groupSize;
    int kernelBendGrass;
    Mesh blade;
    Material groundMaterial;

    Mesh Blade
    {
        get
        {
            Mesh mesh;

            if (blade != null)
            {
                mesh = blade;
            }
            else
            {
                mesh = new Mesh();
                
                float rowHeight = this.height / 4;
                float halfWidth = this.width;

                //1. Use the above variables to define the vertices array
                Vector3[] vertices =
                {
                    new Vector3(-halfWidth, 0, 0),
                    new Vector3( halfWidth, 0, 0),
                    new Vector3(-halfWidth, rowHeight, 0),
                    new Vector3( halfWidth, rowHeight, 0),
                    new Vector3(-halfWidth*0.7f, rowHeight*2, 0),
                    new Vector3( halfWidth*0.7f, rowHeight*2, 0),
                    new Vector3(-halfWidth*0.3f, rowHeight*3, 0),
                    new Vector3( halfWidth*0.3f, rowHeight*3, 0),
                    new Vector3( 0, rowHeight*4, 0)
                };
                //2. Define the normals array, hint: each vertex uses the same normal
                Vector3 normal = new Vector3(0, 0, -1);
                Vector3[] normals =
                {
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal
                };
                //3. Define the uvs array
                Vector2[] uvs =
                {
                    new Vector2(0,0),
                    new Vector2(1,0),
                    new Vector2(0,0.25f),
                    new Vector2(1,0.25f),
                    new Vector2(0,0.5f),
                    new Vector2(1,0.5f),
                    new Vector2(0,0.75f),
                    new Vector2(1,0.75f),
                    new Vector2(0.5f,1)
                };
                //4. Define the indices array
                int[] indices =
                {
                    0,1,2,1,3,2,//row 1
                    2,3,4,3,5,4,//row 2
                    4,5,6,5,7,6,//row 3
                    6,7,8//row 4
                };                
                // int[] indices =
                // {
                //     0,2,1,1,2,3,
                //     2,4,3,3,4,5,
                //     4,6,5,5,6,7,
                //     6,8,7
                // };
                //5. Assign the mesh properties using the arrays
                //   for indices use
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            }

            return mesh;
        }
    }
    // Start is called before the first frame update
    void Start()
    {
        bounds = new Bounds(Vector3.zero, new Vector3(30, 30, 30));
        blade = Blade;

        MeshRenderer renderer = GetComponent<MeshRenderer>();
        groundMaterial = renderer.material;

        InitShader();
    }

    private void OnValidate()
    {
        if (groundMaterial != null)
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();

            renderer.material = (viewNoise) ? visualizeNoise : groundMaterial;

            //TO DO: set wind using wind direction, speed and noise scale
            float theta = windDirection * Mathf.PI / 180;
            Vector4 wind = new Vector4(Mathf.Cos(theta), Mathf.Sin(theta), windSpeed, windScale);
            shader.SetVector("wind", wind);
            visualizeNoise.SetVector("wind", wind);
        }
    }

    void InitShader()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Bounds bounds = mf.sharedMesh.bounds;

        Vector3 blades = bounds.extents;
        Vector3 vec = transform.localScale / 0.1f * density;
        blades.x *= vec.x;
        blades.z *= vec.z;

        int total = (int)blades.x * (int)blades.z * 20;

        kernelBendGrass = shader.FindKernel("BendGrass");

        uint threadGroupSize;
        shader.GetKernelThreadGroupSizes(kernelBendGrass, out threadGroupSize, out _, out _);
        groupSize = Mathf.CeilToInt((float)total / (float)threadGroupSize);
        int count = groupSize * (int)threadGroupSize;

        bladesArray = new GrassBlade[count];
        
        gameObject.AddComponent<MeshCollider>();

        RaycastHit hit;
        Vector3 v = new Vector3();
        Debug.Log(bounds.center.y + bounds.extents.y);
        v.y = (bounds.center.y + bounds.extents.y);
        v = transform.TransformPoint(v);
        float heightWS = v.y;
        v.Set(0, 0, 0);
        v.y = (bounds.center.y - bounds.extents.y);
        v = transform.TransformPoint(v);
        float neHeightWS = v.y;
        float range = heightWS - neHeightWS;
        // heightWS += 10; // 稍微调高一点

        int index = 0;
        int loopCount = 0;
        while (index < count && loopCount < (count * 10))
        {
            loopCount++;
            Vector3 pos = new Vector3( Random.value * bounds.extents.x * 2 - bounds.extents.x + bounds.center.x,
                0,
                Random.value * bounds.extents.z * 2 - bounds.extents.z + bounds.center.z);
            pos = transform.TransformPoint(pos);
            pos.y = heightWS;
            

            if (Physics.Raycast(pos, Vector3.down, out hit))
            {
                pos.y = hit.point.y;
                float deltaHeight = (pos.y - neHeightWS) / range;
                if (Random.value > deltaHeight)
                {
                    GrassBlade blade = new GrassBlade(pos);
                    bladesArray[index++] = blade;
                }
            }
        }
        // for(int i=0; i<count; i++)
        // {
        //     Vector3 pos = new Vector3( Random.value * bounds.extents.x * 2 - bounds.extents.x + bounds.center.x,
        //                                0,
        //                                Random.value * bounds.extents.z * 2 - bounds.extents.z + bounds.center.z);
        //     pos = transform.TransformPoint(pos);
        //     bladesArray[i] = new GrassBlade(pos);
        // }

        bladesBuffer = new ComputeBuffer(count, SIZE_GRASS_BLADE);
        bladesBuffer.SetData(bladesArray);

        shader.SetBuffer(kernelBendGrass, "bladesBuffer", bladesBuffer);
        shader.SetFloat("maxBend", maxBend * Mathf.PI / 180);
        //TO DO: set wind using wind direction, speed and noise scale
        // Vector4 wind = new Vector4();
        float theta = windDirection * Mathf.PI / 180;
        Vector4 wind = new Vector4(Mathf.Cos(theta), Mathf.Sin(theta), windSpeed, windScale);
        shader.SetVector("wind", wind);

        timeID = Shader.PropertyToID("time");

        argsArray[0] = blade.GetIndexCount(0);
        argsArray[1] = (uint)count;
        argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(argsArray);

        material.SetBuffer("bladesBuffer", bladesBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        shader.SetFloat(timeID, Time.time);
        shader.Dispatch(kernelBendGrass, groupSize, 1, 1);

        if (!viewNoise)
        {
            Graphics.DrawMeshInstancedIndirect(blade, 0, material, bounds, argsBuffer);
        }
    }

    private void OnDestroy()
    {
        bladesBuffer.Release();
        argsBuffer.Release();
    }
}
