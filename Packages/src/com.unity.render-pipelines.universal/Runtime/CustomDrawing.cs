using System;

namespace UnityEngine.Rendering.Universal
{
    public class CustomDrawing: Drawing
    {
        public static ScriptableRenderer s_render;
        public delegate void DrawFunction(ScriptableRenderContext context, RenderingData data, ScriptableRenderer render);
        public DrawFunction drawer;
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            drawer(context, renderingData, s_render);
        }
    }
}