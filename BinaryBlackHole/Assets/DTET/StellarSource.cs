using UnityEngine;

namespace DTET.VisualPrototype
{
    // Synthetic emitter in exhibit units, independent of its apparent images. Tint is authored.
    public sealed class StellarSource : MonoBehaviour
    {
        internal bool CachedActive;
        public int SourceId { get; private set; }
        public float DisplayRadius { get; private set; }
        public Color RestTint { get; private set; }

        public void Initialize(int id, float radius, Color tint)
        {
            SourceId = id;
            DisplayRadius = radius;
            RestTint = tint;
            transform.hasChanged = true;
        }
    }
}
