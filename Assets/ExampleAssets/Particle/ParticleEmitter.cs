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
    
    ComputeBuffer particles,quad,pools, counter,indirectdrawbuffer; // counter is used to get the number of the pools
    private ComputeBuffer[] m_pingpongBuffer;
    public Material particalMat;
    public ComputeShader computeShader;
    private int m_currentBufferIndex = 0;
    private int initKernel, emitKernel, updateKernel;
    const int THREAD_COUNT = 256;
    const int particleCount = 255;
    private int bufferSize ;
    private int groupCount;
    private float timer = 0.0f;
    const float emissionRate = 2000.0f;
    private int[] counterArray;
    private int poolsCount = 0;
    CustomDrawing m_drawing;
    #endregion

    #region Unity
    private void OnEnable()
    {
        if (m_drawing == null)
        {
            m_drawing = new CustomDrawing()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox,
            };
            m_drawing.drawer += OnParticlesDrawing;
        }
        if (!ScriptableRenderer.staticDrawingRender.Contains(m_drawing))
            ScriptableRenderer.staticDrawingRender.Add(m_drawing);
        OnInit();
    }

    private void OnDisable()
    {
        if (m_drawing != null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_drawing);
        }
    }
    private void Update()
    {
        float time_delta = Time.deltaTime;
        timer += time_delta;
        computeShader.SetVector("time", new Vector2(time_delta, timer));
        computeShader.SetVector("transportPosition", transform.position);
        computeShader.SetVector("transportForward", transform.forward);
    }

    #endregion

    #region Lifetime
    public float minLifetime = 1f;
    public float maxLifetime = 3f;
    #endregion
    private void OnInit()
    {
        ReleaseBuffer();
        InitBuffers();
        DispatchInit();
        SwapBuffer();
        DispatchUpdate();
    }

    private void SwapBuffer()
    {
        m_currentBufferIndex = 1 - m_currentBufferIndex;
    }

    private void DispatchInit()
    {
        initKernel = computeShader.FindKernel("Init");
        computeShader.SetBuffer(initKernel, "outputs", m_pingpongBuffer[m_currentBufferIndex]);
        computeShader.Dispatch(initKernel, groupCount, 1, 1);
    }

    private void DispatchUpdate()
    {
        updateKernel = computeShader.FindKernel("Update");
        m_pingpongBuffer[m_currentBufferIndex].SetCounterValue(0);
        computeShader.SetBuffer(updateKernel, "outputs", m_pingpongBuffer[m_currentBufferIndex]);
        computeShader.SetBuffer(updateKernel, "inputs", m_pingpongBuffer[1 -m_currentBufferIndex]);
        computeShader.Dispatch(updateKernel, groupCount, 1, 1);
    }

    private void EmitParticles(int count)
    {
        Debug.Log("emit num:" + count);
        emitKernel = computeShader.FindKernel("Emit");
        if (count > 0)
        {
            computeShader.SetFloat("maxCount", particleCount);
            computeShader.SetVector("seeds", new Vector3(Random.Range(1f, 10000f), Random.Range(1f, 10000f), Random.Range(1f, 10000f)));
            computeShader.SetVector("lifeRange", new Vector2(minLifetime, maxLifetime));
            computeShader.SetBuffer(emitKernel, "outputs", m_pingpongBuffer[m_currentBufferIndex]);
            computeShader.Dispatch(emitKernel, count, 1, 1);
        }

    }

    private void CopyIndirectArgs()
    {

    }

    private void DrawParticles(CommandBuffer cmd)
    {                                                                               // cmd.Blit(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget, mat);
        cmd.SetGlobalBuffer("particles", m_pingpongBuffer[m_currentBufferIndex]);
        cmd.SetGlobalBuffer("quad", quad);
        Debug.Log("draw num:" + (bufferSize - poolsCount));
        cmd.DrawProceduralIndirect(Matrix4x4.identity, particalMat, 0, MeshTopology.Triangles, indirectdrawbuffer);
    }

  

    void OnParticlesDrawing(ScriptableRenderContext context)
    {
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            DispatchUpdate();
            EmitParticles(Mathf.RoundToInt(Time.deltaTime * emissionRate));
            CopyIndirectArgs();
            DrawParticles(cmd);
            SwapBuffer();
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    private void InitBuffers()
    {
        groupCount = Mathf.CeilToInt((float)particleCount / THREAD_COUNT);
        bufferSize = groupCount * THREAD_COUNT;
        particles = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Particle)));
        quad = new ComputeBuffer(6, Marshal.SizeOf(typeof(Vector3)));
        pools = new ComputeBuffer(bufferSize, sizeof(int), ComputeBufferType.Append);
        pools.SetCounterValue(0);
        counter = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        indirectdrawbuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        counterArray = new int[] { 6, bufferSize, 0, 0 };
        indirectdrawbuffer.SetData(counterArray);
        quad.SetData(new[]
          {
                new Vector3(0f,0f,0.0f),
                new Vector3(0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f),
                new Vector3(1.0f,1.0f,0.0f),
                new Vector3(0.0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f)
                });
        m_pingpongBuffer = new ComputeBuffer[2];
        m_pingpongBuffer[0] = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Particle)), ComputeBufferType.Append);
        m_pingpongBuffer[1] = new ComputeBuffer(bufferSize, Marshal.SizeOf(typeof(Particle)), ComputeBufferType.Append);
    }

    private void ReleaseBuffer()
    {
        if (particles != null) particles.Release();
        if (quad != null) quad.Release();
        if (pools != null) pools.Release();
        if (counter != null) counter.Release();
        if (indirectdrawbuffer != null) indirectdrawbuffer.Release();
        if(m_pingpongBuffer!= null)
        {
            if (m_pingpongBuffer[0] != null) m_pingpongBuffer[0].Release();
            if (m_pingpongBuffer[1] != null) m_pingpongBuffer[1].Release();
        }
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
