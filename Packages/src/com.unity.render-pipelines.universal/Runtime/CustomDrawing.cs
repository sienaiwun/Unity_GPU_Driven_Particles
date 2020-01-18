using System;

namespace UnityEngine.Rendering.Universal
{
    public class CustomDrawing: Drawing
    {
        public event Action<ScriptableRenderContext> drawer;
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            drawer(context);
        }
    }
}