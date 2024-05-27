using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleFun : MonoBehaviour
{
    private Vector2 cursorPos; // 存储光标位置

    // 粒子结构体定义
    struct Particle
    {
        public Vector3 position; // 粒子位置
        public Vector3 velocity; // 粒子速度
        public float life;       // 粒子生命周期
    }

    const int SIZE_PARTICLE = 7 * sizeof(float); // 单个粒子的大小（以字节为单位）

    public int particleCount = 1000000;  // 粒子数量
    public Material material;            // 渲染粒子使用的材料
    public ComputeShader shader;         // 用于粒子计算的Compute Shader
    [Range(1, 10)]
    public int pointSize = 2;            // 点的大小

    int kernelID;                        // Compute Shader中kernel的ID
    ComputeBuffer particleBuffer;        // 粒子数据缓冲区

    int groupSizeX;                      // shader线程组的大小
    
    
    void Start()
    {
        Init(); // 初始化函数
    }

    void Init()
    {
        // 初始化粒子数组
        Particle[] particleArray = new Particle[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            // 生成随机位置和归一化
            float x = Random.value * 2 - 1.0f;
            float y = Random.value * 2 - 1.0f;
            float z = Random.value * 2 - 1.0f;
            Vector3 xyz = new Vector3(x, y, z);
            xyz.Normalize();
            xyz *= Random.value;
            xyz *= 0.5f;

            // 设置粒子的初始位置和速度
            particleArray[i].position.x = xyz.x;
            particleArray[i].position.y = xyz.y;
            particleArray[i].position.z = xyz.z + 3; // 偏移量为3

            particleArray[i].velocity.x = 0;
            particleArray[i].velocity.y = 0;
            particleArray[i].velocity.z = 0;

            // 设置粒子的生命周期
            particleArray[i].life = Random.value * 5.0f + 1.0f;
        }

        // 创建并设置Compute Buffer
        particleBuffer = new ComputeBuffer(particleCount, SIZE_PARTICLE);
        particleBuffer.SetData(particleArray);

        // 查找Compute Shader中的kernel ID
        kernelID = shader.FindKernel("CSParticle");

        uint threadsX;
        shader.GetKernelThreadGroupSizes(kernelID, out threadsX, out _, out _);
        groupSizeX = Mathf.CeilToInt((float)particleCount / (float)threadsX);

        // 绑定Compute Buffer到shader
        shader.SetBuffer(kernelID, "particleBuffer", particleBuffer);
        material.SetBuffer("particleBuffer", particleBuffer);
        material.SetInt("_PointSize", pointSize);
    }

    void OnRenderObject()
    {
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, 1, particleCount);
    }

    void OnDestroy()
    {
        // 释放Buffer资源
        if (particleBuffer != null)
            particleBuffer.Release();
    }

    void Update()
    {
        float[] mousePosition2D = { cursorPos.x, cursorPos.y };

        // 向Compute Shader发送数据
        shader.SetFloat("deltaTime", Time.deltaTime);
        shader.SetFloats("mousePosition", mousePosition2D);

        // 更新粒子状态
        shader.Dispatch(kernelID, groupSizeX, 1, 1);
    }

    void OnGUI()
    {
        Vector3 p = new Vector3();
        Camera c = Camera.main;
        Event e = Event.current;
        Vector2 mousePos = new Vector2();

        // 获取鼠标位置，并处理Y坐标反转
        mousePos.x = e.mousePosition.x;
        mousePos.y = c.pixelHeight - e.mousePosition.y;

        p = c.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, c.nearClipPlane + 14));

        cursorPos.x = p.x;
        cursorPos.y = p.y;
    }
}
