using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

[ExecuteAlways]
public class ParticleEmitter : MonoBehaviour
{
    #region Const
    static readonly string m_ProfilerTag = "Procedual Particals";
    static readonly string m_DepthboundProfilerTag = "Depth bounds";
    static readonly string m_BeginFrmaeProfilerTag = "Init Frame";
    static readonly string m_EndFrmaeProfilerTag = "End Frame";
    #endregion

    #region varialbe
    private struct Particle
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 data; //x = age, y = lifetime
        public Color color;
        public float size;
        public float alive;
    }
    
   
    public Material particalMat;
    public ComputeShader computeShader;
    public ComputeShader particleSortCS;
    public ComputeShader depthBoundsCS;
    public ComputeShader hizBufferCS;
    public bool enableCuling;
    public bool enableSorting = false;
    public float minLifetime = 1f;
    public float maxLifetime = 3f;

    private int m_currentBufferIndex = 0;
    private int initKernel, emitKernel, updateKernel,copyArgsKernel;
    private int initSortKernel, outerSortKernel,innerSortKernel;
    private int depthboundKernel;
    private int bufferSize;
    private int groupCount;
    private float timer = 0.0f;
    private ComputeBuffer[] m_pingpongBuffer;
    private RenderTargetHandle mipmap8, mipmap16, mipmap32;
    CustomDrawing m_drawing,m_depthBoundDrawing,m_beginFrame,m_endFrame;
    private ComputeBuffer quad, indirectdrawbuffer, dispatchArgsBuffer,indexBuffer; // counter is used to get the number of the pools
    private HiZ depthRangeBuffer;
    const int THREAD_COUNT = 256;
    const int particleCount = 2048*8;//for simplicity, particleCount is the pow(2,xx)*2048
    const float emissionRate = particleCount*0.1f;

    #endregion

    #region Unity
    private void OnEnable()
    {
        if (m_drawing == null)
        {
            m_drawing = AddDrawcall(RenderPassEvent.BeforeRenderingSkybox +1, OnParticlesDrawing);
        }
        if(m_depthBoundDrawing== null)
        {
            m_depthBoundDrawing = AddDrawcall(RenderPassEvent.BeforeRenderingSkybox, OnDepthBounds);
        }
        if(m_beginFrame == null)
        {
            m_beginFrame = AddDrawcall(RenderPassEvent.BeforeRendering, OnBeginFrame);
        }
        if (m_endFrame == null)
        {
            m_endFrame = AddDrawcall(RenderPassEvent.AfterRendering, OnEndFrame);
        }
        OnInit();
    }

    private void OnDisable()
    {
        if (m_drawing != null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_drawing);
        }
        if(m_depthBoundDrawing!=null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_depthBoundDrawing);
        }
        if (m_beginFrame != null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_beginFrame);
        }
        if (m_endFrame != null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_endFrame);
        }
    }



    #endregion
    

    
    private void OnInit()
    {
        ReleaseBuffer();
        InitBuffers();
        DispatchInit();
        SwapBuffer();
        DispatchUpdate();
        depthRangeBuffer = new HiZ();
    }

    private CustomDrawing AddDrawcall(RenderPassEvent rendereveent, CustomDrawing.DrawFunction func)
    {
        CustomDrawing drawing = new CustomDrawing()
        {
            renderPassEvent = rendereveent,
        };
        drawing.drawer = func;
        if (!ScriptableRenderer.staticDrawingRender.Contains(drawing))
            ScriptableRenderer.staticDrawingRender.Add(drawing);
        return drawing;
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
        emitKernel = computeShader.FindKernel("Emit");
        if (count > 0)
        {
            
            computeShader.SetBuffer(emitKernel, "outputs", m_pingpongBuffer[m_currentBufferIndex]);
            computeShader.Dispatch(emitKernel, count, 1, 1);
        }

    }

    private void CopyIndirectArgs()
    {
        ComputeBuffer.CopyCount(m_pingpongBuffer[m_currentBufferIndex], indirectdrawbuffer, 4);
        copyArgsKernel = computeShader.FindKernel("CopyIndirectArgs");
        computeShader.SetBuffer(copyArgsKernel, "drawArgsBuffer", indirectdrawbuffer);
        computeShader.SetBuffer(copyArgsKernel, "dispatchArgsBuffer", dispatchArgsBuffer);
        computeShader.Dispatch(copyArgsKernel, 1, 1, 1);

    }

    private void SortParticles()
    {
        initSortKernel = particleSortCS.FindKernel("ParticleSort");
        particleSortCS.SetBuffer(initSortKernel, "drawArgsBuffer", indirectdrawbuffer);
        particleSortCS.SetBuffer(initSortKernel, "inputs", m_pingpongBuffer[m_currentBufferIndex]);
        particleSortCS.SetBuffer(initSortKernel, "indexBuffer", indexBuffer);
        particleSortCS.DispatchIndirect(initSortKernel, dispatchArgsBuffer);
        if(bufferSize>2048)
        {
            outerSortKernel = particleSortCS.FindKernel("OuterSort");
            innerSortKernel = particleSortCS.FindKernel("InnerSort");
            particleSortCS.SetBuffer(outerSortKernel, "indexBuffer", indexBuffer);
            particleSortCS.SetBuffer(outerSortKernel, "drawArgsBuffer", indirectdrawbuffer);
            particleSortCS.SetBuffer(innerSortKernel, "drawArgsBuffer", indirectdrawbuffer);
            particleSortCS.SetBuffer(innerSortKernel, "indexBuffer", indexBuffer);
            int alignedMaxNumElements = Mathf.NextPowerOfTwo(bufferSize);
            for (int k = 4096; k <= alignedMaxNumElements; k *= 2)
            {
                particleSortCS.SetInt("k", k);
                for (int j = k / 2; j >= 2048; j /= 2)
                {
                    particleSortCS.SetInt("j", j);
                    particleSortCS.DispatchIndirect(outerSortKernel, dispatchArgsBuffer);
                }
                particleSortCS.DispatchIndirect(innerSortKernel, dispatchArgsBuffer);
            }

        }
    }

    private void UpdateParticles(RenderingData data)
    {
        Camera mainCamera = data.cameraData.camera;
        Matrix4x4 vp = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false) * mainCamera.worldToCameraMatrix;
        float time_delta = Time.deltaTime;
        timer += time_delta;

        computeShader.SetVector("time", new Vector2(time_delta, timer));
        computeShader.SetVector("transportPosition", transform.position);
        computeShader.SetVector("transportForward", transform.forward);
        computeShader.SetFloat("maxCount", particleCount);
        computeShader.SetVector("seeds", new Vector3(Random.Range(1f, 10000f), Random.Range(1f, 10000f), Random.Range(1f, 10000f)));
        computeShader.SetVector("lifeRange", new Vector2(minLifetime, maxLifetime));
        computeShader.SetMatrix("gViewProj", vp);
        computeShader.SetBool("enableCulling", enableCuling);
        computeShader.SetBool("enableSorting", enableSorting);
        particleSortCS.SetMatrix("gViewProj", vp);

        DispatchUpdate();
        EmitParticles(Mathf.RoundToInt(Time.deltaTime * emissionRate));
        CopyIndirectArgs();
        if (enableSorting) // after buffer swap,
        {
            SortParticles();
        }
        SwapBuffer();
        
    }

    void DepthBoundCalculation(CommandBuffer cmd,  RenderingData data, ScriptableRenderer render)
    {
        ForwardRenderer forwardRenderer = render as ForwardRenderer;
        depthboundKernel = depthBoundsCS.FindKernel("CSMain");
        cmd.SetComputeTextureParam(depthBoundsCS, depthboundKernel, "DepthTexture", forwardRenderer.ActiveCameraDepthRT);
        int screenWidth = data.cameraData.cameraTargetDescriptor.width;
        int screenHeight = data.cameraData.cameraTargetDescriptor.height;      
        cmd.SetComputeTextureParam(depthBoundsCS, depthboundKernel, "mipmap8", mipmap8.Identifier());
        cmd.SetComputeTextureParam(depthBoundsCS, depthboundKernel, "mipmap16", mipmap16.Identifier());
        cmd.SetComputeTextureParam(depthBoundsCS, depthboundKernel, "mipmap32", mipmap32.Identifier());
        cmd.SetComputeFloatParams(depthBoundsCS, "gRcpBufferDim", new float[] { 1.0f / screenWidth, 1.0f / screenHeight });
        cmd.DispatchCompute(depthBoundsCS, depthboundKernel, screenWidth, screenHeight, 1);
 
    }

    void OnDepthBounds(ScriptableRenderContext context, RenderingData data, ScriptableRenderer render)
    {
        if (!enableCuling)
            return;
        CommandBuffer cmd = CommandBufferPool.Get(m_DepthboundProfilerTag);
        using (new ProfilingSample(cmd, m_DepthboundProfilerTag))
        {
            ForwardRenderer forwardRenderer = render as ForwardRenderer;
            Material hizMaterial = new Material(Shader.Find("Hidden/Custom/HizBlit"));
            depthRangeBuffer.GeneragteHizTexture(cmd, forwardRenderer.ActiveCameraDepthRT,hizBufferCS);
            DepthBoundCalculation(cmd, data, render);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    void OnBeginFrame(ScriptableRenderContext context, RenderingData data, ScriptableRenderer render)
    {
        if (!enableCuling)
            return;
        int screenWidth = data.cameraData.cameraTargetDescriptor.width;
        int screenHeight = data.cameraData.cameraTargetDescriptor.height;
        CommandBuffer cmd = CommandBufferPool.Get(m_BeginFrmaeProfilerTag);
        int bufferWidth8 = (screenWidth + 7) / 8;
        int bufferWidth16 = (screenWidth + 15) / 16;
        int bufferWidth32 = (screenWidth + 31) / 32;
        int bufferHeight8 = (screenHeight + 7) / 8;
        int bufferHeight16 = (screenHeight + 15) / 16;
        int bufferHeight32 = (screenHeight + 31) / 32;
        using (new ProfilingSample(cmd, m_BeginFrmaeProfilerTag))
        {
            RenderTextureDescriptor desc8 = new RenderTextureDescriptor
            {
                width = bufferWidth8,
                height = bufferHeight8,
                graphicsFormat = GraphicsFormat.R32_SFloat,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                bindMS = false,
                msaaSamples = 1,
                useMipMap = false,
            };
            RenderTextureDescriptor desc16 = new RenderTextureDescriptor
            {
                width = bufferWidth16,
                height = bufferHeight16,
                graphicsFormat = GraphicsFormat.R32_SFloat,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                bindMS = false,
                msaaSamples = 1,
                useMipMap = false,
            };
            RenderTextureDescriptor desc32 = new RenderTextureDescriptor
            {
                width = bufferWidth32,
                height = bufferHeight32,
                graphicsFormat = GraphicsFormat.R32_SFloat,
                enableRandomWrite = true,
                dimension = TextureDimension.Tex2D,
                bindMS = false,
                msaaSamples = 1,
                useMipMap = false,
            };

            cmd.GetTemporaryRT(mipmap8.id, desc8);
            cmd.GetTemporaryRT(mipmap16.id, desc16);
            cmd.GetTemporaryRT(mipmap32.id, desc32);
        }
        depthRangeBuffer.InitHiz(cmd, screenWidth, screenHeight);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    void OnEndFrame(ScriptableRenderContext context, RenderingData data, ScriptableRenderer render)
    {
        if (!enableCuling)
            return;
        int screenWidth = data.cameraData.cameraTargetDescriptor.width;
        int screenHeight = data.cameraData.cameraTargetDescriptor.height;
        CommandBuffer cmd = CommandBufferPool.Get(m_EndFrmaeProfilerTag);
        using (new ProfilingSample(cmd, m_EndFrmaeProfilerTag))
        {
            cmd.ReleaseTemporaryRT(mipmap8.id);
            cmd.ReleaseTemporaryRT(mipmap16.id);
            cmd.ReleaseTemporaryRT(mipmap32.id);
            depthRangeBuffer.OnPostRenderHiz(cmd);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    void OnParticlesDrawing(ScriptableRenderContext context, RenderingData data, ScriptableRenderer render)
    {
        UpdateParticles(data);
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            ForwardRenderer forwardRenderer = render as ForwardRenderer;
            cmd.SetRenderTarget(forwardRenderer.ActiveCameraColorRT, forwardRenderer.ActiveCameraDepthRT);
            cmd.SetGlobalBuffer("particles", m_pingpongBuffer[1 - m_currentBufferIndex]);
            cmd.SetGlobalBuffer("quad", quad);
            if(enableSorting)
            {
                cmd.SetGlobalBuffer("indexBuffer", indexBuffer);
                particalMat.EnableKeyword("ENABLE_SORTINT");
            }
            else
            {
                particalMat.DisableKeyword("ENABLE_SORTINT");
            }
            cmd.DrawProceduralIndirect(Matrix4x4.identity, particalMat, 0, MeshTopology.Triangles, indirectdrawbuffer);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }

    private void InitBuffers()
    {
        groupCount = Mathf.CeilToInt((float)particleCount / THREAD_COUNT);
        bufferSize = groupCount * THREAD_COUNT;
        quad = new ComputeBuffer(6, Marshal.SizeOf(typeof(Vector3)));
        indirectdrawbuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        dispatchArgsBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.IndirectArguments);
        indirectdrawbuffer.SetData(new int[] { 6, 0, 0, 0 });
        dispatchArgsBuffer.SetData(new int[] { 0, 1, 1 });
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
        indexBuffer = new ComputeBuffer(bufferSize, sizeof(int), ComputeBufferType.Raw);
        mipmap8 = new RenderTargetHandle();
        mipmap16 = new RenderTargetHandle();
        mipmap32 = new RenderTargetHandle();
        mipmap8.Init("mipmap8");
        mipmap16.Init("mipmap16");
        mipmap32.Init("mipmap32");
    }

    private void ReleaseBuffer()
    {
        if (quad != null) quad.Release();
        if (indirectdrawbuffer != null) indirectdrawbuffer.Release();
        if (dispatchArgsBuffer != null) dispatchArgsBuffer.Release();
        if (indexBuffer != null) indexBuffer.Release();
        if (m_pingpongBuffer!= null)
        {
            if (m_pingpongBuffer[0] != null) m_pingpongBuffer[0].Release();
            if (m_pingpongBuffer[1] != null) m_pingpongBuffer[1].Release();
        }
    }
    
}
