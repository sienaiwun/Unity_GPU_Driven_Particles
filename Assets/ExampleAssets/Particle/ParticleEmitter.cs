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
    }

    Particle[] m_CpuPartucleBuffer;
    ComputeBuffer particles,quad;
    Material particalMat;
    public int particleCount = 20;
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

    }

    #endregion

    private void OnInit()
    {
        ReleaseBuffer();
        particalMat = new Material(Shader.Find("Custom/Billboard Particles"));
        particles = new ComputeBuffer(particleCount,  Marshal.SizeOf(typeof(Particle)));
        quad = new ComputeBuffer(6, Marshal.SizeOf(typeof(Vector3)));

        quad.SetData(new[]
          {
                new Vector3(0f,0f,0.0f),
                new Vector3(0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f),
                new Vector3(1.0f,1.0f,0.0f),
                new Vector3(0.0f,1.0f,0.0f),
                new Vector3(1.0f,0.0f,0.0f)
                });


        m_CpuPartucleBuffer = new Particle[particleCount];
    }

   

    void OnEndCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        
        for (int index = 0; index < particleCount; index++)
        {
            m_CpuPartucleBuffer[index].position = gameObject.transform.position;
            m_CpuPartucleBuffer[index].forward = gameObject.transform.forward;
            m_CpuPartucleBuffer[index].color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            m_CpuPartucleBuffer[index].size = Random.Range(0.0f, 0.5f);
            m_CpuPartucleBuffer[index].data.z = Random.Range(0,1.0f);

        }
        particles.SetData(m_CpuPartucleBuffer);
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
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
