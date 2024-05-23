using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 要求附加在有Camera组件的GameObject上
[RequireComponent(typeof(Camera))]
public class BasePP : MonoBehaviour
{
    // 计算着色器的引用
    public ComputeShader shader = null;

    // 计算着色器中主内核函数的名称
    protected string kernelName = "CSMain";

    // 纹理的尺寸
    protected Vector2Int texSize = new Vector2Int(0,0);
    // 计算着色器的线程组大小
    protected Vector2Int groupSize = new Vector2Int();
    // 指向相机组件的引用
    protected Camera thisCamera;

    // 输出用的渲染纹理
    protected RenderTexture output = null;
    // 原始图像的渲染纹理
    protected RenderTexture renderedSource = null;

    // 计算着色器内核的句柄
    protected int kernelHandle = -1;
    // 初始化标志
    protected bool init = false;

    // 初始化后处理效果的方法
    protected virtual void Init()
    {
        // 检查是否支持计算着色器
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("It seems your target Hardware does not support Compute Shaders.");
            return;
        }

        // 检查着色器是否赋值
        if (!shader)
        {
            Debug.LogError("No shader");
            return;
        }

        // 获取着色器内核的句柄
        kernelHandle = shader.FindKernel(kernelName);

        // 获取Camera组件
        thisCamera = GetComponent<Camera>();

        if (!thisCamera)
        {
            Debug.LogError("Object has no Camera");
            return;
        }

        // 创建必要的纹理
        CreateTextures();

        // 标记为初始化完成
        init = true;
    }

    // 清理指定的渲染纹理
    protected void ClearTexture(ref RenderTexture textureToClear)
    {
        if (null != textureToClear)
        {
            textureToClear.Release();
            textureToClear = null;
        }
    }

    // 清理所有纹理资源
    protected virtual void ClearTextures()
    {
        ClearTexture(ref output);
        ClearTexture(ref renderedSource);
    }

    // 创建一个渲染纹理
    protected void CreateTexture(ref RenderTexture textureToMake, int divide=1)
    {
        textureToMake = new RenderTexture(texSize.x/divide, texSize.y/divide, 0);
        textureToMake.enableRandomWrite = true;
        textureToMake.Create();
    }

    // 创建所需的纹理并设置它们的尺寸和绑定
    protected virtual void CreateTextures()
    {
        texSize.x = thisCamera.pixelWidth;
        texSize.y = thisCamera.pixelHeight;

        if (shader)
        {
            uint x, y;
            shader.GetKernelThreadGroupSizes(kernelHandle, out x, out y, out _);
            groupSize.x = Mathf.CeilToInt((float)texSize.x / (float)x);
            groupSize.y = Mathf.CeilToInt((float)texSize.y / (float)y);
        }

        CreateTexture(ref output);
        CreateTexture(ref renderedSource);

        shader.SetTexture(kernelHandle, "source", renderedSource);
        shader.SetTexture(kernelHandle, "outputrt", output);
    }

    // 在启用时初始化
    protected virtual void OnEnable()
    {
        Init();
    }

    // 在禁用时清理资源
    protected virtual void OnDisable()
    {
        ClearTextures();
        init = false;
    }

    // 在销毁时清理资源
    protected virtual void OnDestroy()
    {
        ClearTextures();
        init = false;
    }

    // 用计算着色器处理图像
    protected virtual void DispatchWithSource(ref RenderTexture source, ref RenderTexture destination)
    {
        Graphics.Blit(source, renderedSource);

        shader.Dispatch(kernelHandle, groupSize.x, groupSize.y, 1);
        
        Graphics.Blit(output, destination);
    }

    // 检查分辨率是否改变
    protected void CheckResolution(out bool resChange )
    {
        resChange = false;

        if (texSize.x != thisCamera.pixelWidth || texSize.y != thisCamera.pixelHeight)
        {
            resChange = true;
            CreateTextures();
        }
    }
    
    // Unity的渲染事件，处理和渲染图像
    protected virtual void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!init || shader == null)
        {
            Graphics.Blit(source, destination);
        }
        else
        {
            CheckResolution(out _);
            DispatchWithSource(ref source, ref destination);
        }
    }
}
