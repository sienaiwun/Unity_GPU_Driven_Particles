using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class CustomOpaqueRender : MonoBehaviour
{
    #region Const
    static readonly string m_ProfilerTag = "CustomOpaqueRender";
    #endregion


    #region variables
    CustomDrawing m_drawing;
    Material m_material;
    #endregion


    #region Unity
    private void OnEnable()
    {
        if(m_drawing == null)
        {
            m_drawing = new CustomDrawing()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques,
            };
            m_drawing.drawer += OnTransperentDrawing;
        }
        m_material = new Material(Shader.Find("Hidden/Universal Render Pipeline/BlitTest"));
        if(!ScriptableRenderer.staticDrawingRender.Contains(m_drawing))
            ScriptableRenderer.staticDrawingRender.Add(m_drawing); 
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
       
    }
    #endregion
  
    void OnTransperentDrawing(ScriptableRenderContext context)
    {

        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,
                      RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,     // color
                      RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare); // depth
            cmd.DrawProcedural(Matrix4x4.identity, m_material, 0, MeshTopology.Triangles, 6, 10);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }
    
    
}
