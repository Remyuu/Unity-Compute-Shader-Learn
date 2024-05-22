using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AssignTexture : MonoBehaviour
{
    // ComputeShader 用于在 GPU 上执行计算任务
    public ComputeShader shader;

    // 纹理分辨率
    public int texResolution = 256;

    public string kernelName = "CSMain";

    // 渲染器组件
    private Renderer rend;
    // 渲染纹理
    private RenderTexture outputTexture;
    // 计算着色器内核句柄
    private int kernelHandle;
    
    // Start 在脚本启用时被调用一次
    void Start()
    {
        // 创建一个新的渲染纹理，指定宽度、高度和位深度（此处位深度为0）
        outputTexture = new RenderTexture(texResolution, texResolution, 0);
        // 允许随机写入
        outputTexture.enableRandomWrite = true;
        // 创建渲染纹理实例
        outputTexture.Create();

        // 获取当前对象的渲染器组件
        rend = GetComponent<Renderer>();
        // 启用渲染器
        rend.enabled = true;
        
        InitShader();
    }

    private void InitShader()
    {
        // 查找计算着色器内核 "CSMain" 的句柄
        kernelHandle = shader.FindKernel(kernelName);
        shader.SetInt("texResolution", texResolution);
        
        // 设置计算着色器中使用的纹理
        shader.SetTexture(kernelHandle, "Result", outputTexture);

        // 将渲染纹理设置为材质的主纹理
        rend.material.SetTexture("_MainTex", outputTexture);

        // 调度计算着色器的执行，传入计算组的大小
        // 这里假设每个工作组是 16x16
        // 简单的说就是，要分配多少个组，才能完成计算，目前只分了xy的各一半，因此只渲染了1/4的画面。
        DispatchShader(texResolution / 16, texResolution / 16);
    }
    
    private void DispatchShader(int x, int y)
    {
        // 调度计算着色器的执行
        // x 和 y 表示计算组的数量，1 表示 z 方向上的计算组数量（这里只有一个）
        shader.Dispatch(kernelHandle, x, y, 1);
    }

    void Update()
    {
        // 每帧检查是否有键盘输入（按键 U 被松开）
        if (Input.GetKeyUp(KeyCode.U))
        {
            // 如果按键 U 被松开，则重新调度计算着色器
            DispatchShader(texResolution / 8, texResolution / 8);
        }
    }
}