using UnityEngine;

namespace DTET.Guide
{
    public static class GuideAutoStart
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (Object.FindFirstObjectByType<GuideUiHost>() != null)
            {
                return;
            }

            new GameObject("GuideUI").AddComponent<GuideUiHost>();
        }
    }
}