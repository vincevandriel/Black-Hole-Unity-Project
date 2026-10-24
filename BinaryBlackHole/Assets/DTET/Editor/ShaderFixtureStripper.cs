using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace DTET.VisualPrototype.Editor
{
    // Keep numerical shader fixtures out of distributable players.
    public sealed class ShaderFixtureStripper : IPreprocessShaders
    {
        internal static bool KeepFixtures;
        public int callbackOrder => 0;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader.name != "DTET/InteractiveOptics" && shader.name != "DTET/FeaturedBackgroundStar"
                && shader.name != "DTET/GeneratedFluidVolume" && shader.name != "DTET/ReducedBinaryVolume"
                && shader.name != "DTET/ReferenceVolume") return;
            var keyword = new ShaderKeyword(shader, "DTET_QA_FIXTURES");
            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
                if (!KeepFixtures && data[i].shaderKeywordSet.IsEnabled(keyword)) { data.RemoveAt(i); removed++; }
            Debug.Log($"DTET_SHADER_VARIANTS {shader.name} fixtures={KeepFixtures} removed={removed} retained={data.Count}");
        }
    }
}
