Shader "Custom/GrassBlades"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _BottomColor ("草根色", Color) = (1,1,1,1)
        _TopColor ("草尖色", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" }
        
		LOD 200
		Cull Off
		
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types   
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma instancing_options procedural:setup

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _BottomColor;
        fixed4 _TopColor;
        float _Fade;
        float4x4 _Matrix;
        float3 _Position;

        float4x4 create_matrix(float3 pos, float theta){
            float c = cos(theta);
            float s = sin(theta);
            return float4x4(
                c,-s, 0, pos.x,
                s, c, 0, pos.y,
                0, 0, 1, pos.z,
                0, 0, 0, 1
            );
        }
        float3x3 transpose(float3x3 m)
        {
            return float3x3(
                float3(m[0][0], m[1][0], m[2][0]), // Column 1
                float3(m[0][1], m[1][1], m[2][1]), // Column 2
                float3(m[0][2], m[1][2], m[2][2])  // Column 3
            );
        }

        float4x4 AngleAxis4x4(float3 pos, float angle, float3 axis){
            float c, s;
            sincos(angle*2*3.14, s, c);

            float t = 1 - c;
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;

            return float4x4(
                t * x * x + c    , t * x * y - s * z, t * x * z + s * y, pos.x,
                t * x * y + s * z, t * y * y + c    , t * y * z - s * x, pos.y,
                t * x * z - s * y, t * y * z + s * x, t * z * z + c    , pos.z,
                0,0,0,1
                );
        }
        
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            struct GrassBlade
            {
                float3 position;
                float bend;
                float noise;
                float fade;
                float face;
            };
            StructuredBuffer<GrassBlade> bladesBuffer; 
        #endif

        void vert(inout appdata_full v, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // 应用模型变换
                v.vertex = mul(_Matrix, v.vertex);
            
                // 计算逆转置矩阵用于法线变换
                float3x3 normalMatrix = (float3x3)transpose(((float3x3)_Matrix));
                // 变换法线
                v.normal = mul(normalMatrix, v.normal);
            #endif
        }

        void setup()
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                GrassBlade blade = bladesBuffer[unity_InstanceID];
                _Fade = blade.fade;
                // 创建绕Y轴的旋转矩阵（面向）
                float4x4 rotationMatrixY = AngleAxis4x4(blade.position, blade.face, float3(0,1,0));
                // float4x4 rotationMatrixY = AngleAxis4x4(float3(0,0,0), float(_Fa), float3(0,1,0));
                // 创建绕X轴的旋转矩阵（倾倒）
                float4x4 rotationMatrixX = AngleAxis4x4(float3(0,0,0), blade.bend, float3(1,0,0));
                // 合成两个旋转矩阵
                _Matrix = mul(rotationMatrixY, rotationMatrixX);
                // 设置位置
                _Position = blade.position;
            #endif
        }


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color * _Fade * lerp(_BottomColor, _TopColor, IN.uv_MainTex.y);
            o.Albedo = c.rgb;
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
