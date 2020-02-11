using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HiZ : MonoBehaviour
{
    #region Variables
    public RenderTexture HizDepthTexture = null;
    private int m_size, m_miplevel;
    private int[] m_temporalTexHandle;
    private enum Pass
    {
        Blit,
        Reduce
    }
    #endregion

    public int Size    {  get { return m_size; }   }
    public int Lodlevel { get { return m_miplevel; } }


    public void InitHiz(CommandBuffer cmd,int _width,int _height)
    {
        int size = (int)Mathf.Max((float)_width, (float)_height);
        size = (int)Mathf.NextPowerOfTwo(size);
        m_size = size;
        m_miplevel = (int)Mathf.Floor(Mathf.Log(size, 2f));
        if (m_miplevel == 0)
        {
            return;
        }
        RenderTextureDescriptor desc = new RenderTextureDescriptor()
        {
            width = size,
            height = size,
            autoGenerateMips = false,
            useMipMap = true,
            colorFormat = RenderTextureFormat.RFloat,
            dimension = TextureDimension.Tex2D,
            bindMS = false,
            msaaSamples = 1,
            volumeDepth = 1,
        };
        HizDepthTexture = RenderTexture.GetTemporary(desc);
    }
   
    

    public RenderTexture GeneragteHizTexture(CommandBuffer cmd, RenderTargetIdentifier source, ComputeShader HizCS)
    {
        m_temporalTexHandle = new int[m_miplevel];
        for (int i= 0;i<m_miplevel;i++)
        {
            m_temporalTexHandle[i] = Shader.PropertyToID("tempory depth buffer"+i);
            int temporalsize = m_size >> i;
            temporalsize = Mathf.Max(temporalsize, 1);
            RenderTextureDescriptor temp_desc = new RenderTextureDescriptor()
            {
                width = temporalsize,
                height = temporalsize,
                autoGenerateMips = false,
                useMipMap = true,
                enableRandomWrite = true,
                colorFormat = RenderTextureFormat.RFloat,
                dimension = TextureDimension.Tex2D,
                bindMS = false,
                msaaSamples = 1,
            };
            cmd.GetTemporaryRT(m_temporalTexHandle[i], temp_desc, FilterMode.Point);
            cmd.SetComputeFloatParams(HizCS, "gRcpBufferDim", new float[] { 1.0f / temporalsize, 1.0f / temporalsize });
            if (i == 0)
            {
                // cmd.Blit(source, m_temporalTexHandle[i], hizGenerateMeterial, (int)Pass.Blit);
                int blitKernel = HizCS.FindKernel("Blit");
                cmd.SetComputeTextureParam(HizCS, blitKernel, "Input", source);
                cmd.SetComputeTextureParam(HizCS, blitKernel, "Output", m_temporalTexHandle[i]);
                cmd.DispatchCompute(HizCS, blitKernel, temporalsize / 8, temporalsize / 8, 1);
            }
            else
            { 
                int gatherKernel = HizCS.FindKernel("Gather");
                cmd.SetComputeTextureParam(HizCS, gatherKernel, "Input", m_temporalTexHandle[i - 1]);
                cmd.SetComputeTextureParam(HizCS, gatherKernel, "Output", m_temporalTexHandle[i]);
                int groupSize = Mathf.Max(1, temporalsize / 8);
                cmd.DispatchCompute(HizCS, gatherKernel, groupSize, groupSize, 1);
                cmd.ReleaseTemporaryRT(m_temporalTexHandle[i - 1]);
            }
            cmd.CopyTexture(m_temporalTexHandle[i], 0, 0, HizDepthTexture, 0, i );
        }
        cmd.ReleaseTemporaryRT(m_temporalTexHandle[m_miplevel-1]);
        return HizDepthTexture;
    }

    public void OnPostRenderHiz(CommandBuffer cmd)
    {
        RenderTexture.ReleaseTemporary(HizDepthTexture);
    }
}
