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
    
    ComputeBuffer particles,quad,pools, counter; // counter is used to get the number of the pools
    Material particalMat;
    public ComputeShader computeShader;

    private int initKernel, emitKernel, updateKernel;
    const int THREAD_COUNT = 256;
    public int particleCount = 20;
    private int bufferSize ;
    private int groupCount;
    private float timer = 0.0f;
    private float emissionRate = 10;
    private int[] counterArray;
    private int poolsCount = 0;
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
        float time_delta = Time.deltaTime;
        timer += time_delta;
        computeShader.SetVector("time", new Vector2(time_delta, timer));
        computeShader.SetVector("transportPosition", transform.position);
        computeShader.SetVector("transportForward", transform.forward);
        EmitParticles(Mathf.RoundToInt(time_delta * emissionRate));
    }

    #endregion

    private void OnInit()
    {
        ReleaseBuffer();
        DispatchInit();
    }

    void EmitParticles(int count)
    {
        emitKernel = computeShader.FindKernel("Emit");

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
            counter = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            counterArray = new int[] { 0, 1, 0, 0 };
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
            computeShader.SetBuffer(updateKernel, "pools", pools);
            SetPoolsCount();

        }
    }
   

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
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

    private int GetPoolCount()
    {
        return poolsCount;
    }

    private void SetPoolsCount()
    {
        if (pools == null || counter == null || counterArray == null)
        {
            poolsCount = bufferSize;
            return;
        }
        counter.SetData(counterArray);
        ComputeBuffer.CopyCount(pools, counter, 0);
        counter.GetData(counterArray);
        poolsCount = counterArray[0];
    }
}
