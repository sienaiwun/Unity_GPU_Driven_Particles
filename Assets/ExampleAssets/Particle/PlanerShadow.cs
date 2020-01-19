
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class PlanerShadow : MonoBehaviour
{
    #region Const
    static readonly string m_ProfilerTag = "CustomOpaqueRender";
    #endregion


    #region variables
    public Material m_material;
    public GameObject planerLight;
    CustomDrawing m_drawing;
    int m_planerShadowDirParam;
    Transform m_planerShadowDirTransform;
    Dictionary<int, int> m_meshToTransform;
    #endregion
    
    #region Unity
    private void OnEnable()
    {
        if(m_drawing == null)
        {
            m_drawing = new CustomDrawing()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox,
            };
            m_drawing.drawer += OnRenderObjectSRP;
        }
        m_planerShadowDirParam = Shader.PropertyToID("_planerLightDir");
        MeshFilter[] meshFileters = GetComponentsInChildren<MeshFilter>();
        Transform[] trans = GetComponentsInChildren<Transform>();
        m_meshToTransform = new Dictionary<int, int>();
        for (int i = 0; i < meshFileters.Length; i++)
        {
            for (int j = 0; j < trans.Length; j++)
            {
                if (meshFileters[i].name == trans[j].name)
                {
                    m_meshToTransform.Add(i, j);
                    break;
                }
            }
        }
        if (planerLight)
            m_planerShadowDirTransform = planerLight.GetComponent<Transform>();
        if (!ScriptableRenderer.staticDrawingRender.Contains(m_drawing))
            ScriptableRenderer.staticDrawingRender.Add(m_drawing); 
    }
    private void OnDisable()
    {
        if (m_drawing != null)
        {
            UnityEngine.Rendering.Universal.ForwardRenderer.staticDrawingRender.Remove(m_drawing);
        }
    }

    #endregion

    void DrawMeshes(CommandBuffer cmd)
    {
        MeshFilter[] meshFileters = GetComponentsInChildren<MeshFilter>();
        Transform[] trans = GetComponentsInChildren<Transform>();
        for (int i = 0; i < meshFileters.Length; i++)
        {
            cmd.DrawMesh(meshFileters[i].sharedMesh, trans[m_meshToTransform[i]].localToWorldMatrix, m_material);
        }
    }
    
    void OnRenderObjectSRP(ScriptableRenderContext context)
    {
        if (m_material == null)
            return;
        CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
        using (new ProfilingSample(cmd, m_ProfilerTag))
        {
            if(m_planerShadowDirTransform)
                m_material.SetVector(m_planerShadowDirParam, m_planerShadowDirTransform.forward);
            DrawMeshes(cmd);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
        context.Submit();
    }
    
    
}
