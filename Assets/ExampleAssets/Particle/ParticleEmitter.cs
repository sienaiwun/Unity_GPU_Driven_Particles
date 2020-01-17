using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class ParticleEmitter : MonoBehaviour
{
    #region Const
    static readonly string m_ProfilerTag = "Procedual Particals";
    #endregion

    #region varialbe
    private struct Particle
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 data; //x = age, y = lifetime
        public Color color;
        public float size;
        bool alive;
    }
    
    ComputeBuffer particles,quad,pools;
    Material particalMat;
    public ComputeShader computeShader;

    private int initKernel, emitKernel, updateKernel;
    const int THREAD_COUNT = 256;
    public int particleCount = 20;
    private int bufferSize ;
    private int groupCount;
    #endregion

    #region Unity
    private void OnEnable()
    {
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeforeCameraRendering;
        OnInit();
    }

    private void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeforeCameraRendering;
    }
    private void Update()
    {
        computeShader.SetVector("transportPosition", transform.position);
        computeShader.SetVector("transportForward", transform.forward);
    }

    #endregion

    private void OnInit()
    {
        ReleaseBuffer();
        DispatchInit();
    }

   
    void DispatchInit()
    {
        // init 
        {
            initKernel = computeShader.FindKernel("Init");
            particalMat = new Material(Shader.Find("Custom/Billboard Particles"));
            groupCount = Mathf.CeilToInt((float)particleCount / THREAD_COUNT);
            bufferSize = (groupCount+1) * THREAD_COUNT;
            particles = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Particle)));
            quad = new ComputeBuffer(6, Marshal.SizeOf(typeof(Vector3)));
            pools = new ComputeBuffer(bufferSize, sizeof(int), ComputeBufferType.Append);
            pools.SetCounterValue(0);
            quad.SetData(new[]
              {
                new Vector3(0f,0f,0.0f),
                new Vector3(0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f),
                new Vector3(1.0f,1.0f,0.0f),
                new Vector3(0.0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f)
                });
            initKernel = computeShader.FindKernel("Init");
            computeShader.SetBuffer(initKernel, "particles", particles);
            computeShader.SetBuffer(initKernel, "pools", pools);
            computeShader.Dispatch(initKernel, groupCount, 1, 1);

            updateKernel = computeShader.FindKernel("Update");
            computeShader.SetBuffer(updateKernel, "particles", particles);

        }
    }

    void OnEndCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        
        
      
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            updateKernel = computeShader.FindKernel("Update");
            computeShader.Dispatch(updateKernel, groupCount, 1, 1);
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                      RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,     // color
                      RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
                                                               // cmd.Blit(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, mat);
            cmd.SetGlobalBuffer("particles", particles);
            cmd.SetGlobalBuffer("quad", quad);
            cmd.DrawProcedural(Matrix4x4.identity, particalMat, 0, MeshTopology.Triangles, 6, 10);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    void OnBeforeCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {

    }

    private void ReleaseBuffer()
    {
        if (particles != null) particles.Release();
        if (quad != null) quad.Release();
    }
}
