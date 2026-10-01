#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Esnaf.App
{
    /// <summary>
    /// Üst menüde "Esnaf > Test: ..." (yalnızca Play Mode'da etkin): sahnedeki GameBootstrap'in el ile deneme yardımcısını çağırır.
    /// Bileşen menüsündeki (⋮) ContextMenu ile aynı işi yapar; Inspector'a bağlı olmadığı için her zaman bulunur. Oyun kuralı yoktur.
    /// </summary>
    internal static class GameBootstrapTestMenu
    {
        private const string Title = "Esnaf/Test: ilk ilani al ve etiketle (musteri gelsin)";

        [MenuItem(Title)]
        private static void Run()
        {
            GameBootstrap bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("Esnaf: sahnede GameBootstrap yok (Main sahnesini açıp Play'e basın).");
                return;
            }

            bootstrap.DebugBuyAndPriceFirstListing();
        }

        [MenuItem(Title, true)]
        private static bool CanRun()
        {
            return EditorApplication.isPlaying;
        }
    }
}
#endif
