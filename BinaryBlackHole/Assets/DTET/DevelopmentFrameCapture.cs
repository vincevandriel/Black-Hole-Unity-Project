#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype
{
    // QA camera capture, independent of the window backbuffer.
    internal static class DevelopmentFrameCapture
    {
        public static void WritePng(Camera camera, string path, int width = 1600, int height = 900)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = RenderTexture.active;
            var previousAspect = camera.aspect;
            Texture2D pixels = null;
            try
            {
                camera.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("Current pipeline does not support the QA camera render request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.aspect = previousAspect;
                RenderTexture.active = previousTarget;
                if (pixels != null) UnityEngine.Object.Destroy(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
