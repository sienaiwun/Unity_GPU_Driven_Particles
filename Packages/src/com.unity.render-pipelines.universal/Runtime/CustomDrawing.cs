using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;


namespace UnityEngine.Rendering.Universal
{
    public class CustomDrawing
    {
        public RenderPassEvent renderPassEvent { get; set; }
        public event Action<ScriptableRenderContext> drawer;
        public static bool operator <(CustomDrawing lhs, CustomDrawing rhs)
        {
            return lhs.renderPassEvent < rhs.renderPassEvent;
        }

        public static bool operator >(CustomDrawing lhs, CustomDrawing rhs)
        {
            return lhs.renderPassEvent > rhs.renderPassEvent;
        }

        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            drawer(context);
        }

    }
}