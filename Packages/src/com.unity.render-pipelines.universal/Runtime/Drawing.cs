using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;


namespace UnityEngine.Rendering.Universal
{
    public abstract class Drawing 
    {
        public RenderPassEvent renderPassEvent { get; set; }
        public static bool operator <(Drawing lhs, Drawing rhs)
        {
            return lhs.renderPassEvent < rhs.renderPassEvent;
        }

        public static bool operator >(Drawing lhs, Drawing rhs)
        {
            return lhs.renderPassEvent > rhs.renderPassEvent;
        }

        public virtual void FrameCleanup(CommandBuffer cmd)
        { }

        public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);
    }
}
