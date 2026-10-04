using System;
using System.IO;
using UnityEngine;

namespace Lalim.Core
{
    /// <summary>
    /// Settings ▸ System ▸ Others. Lalim ships all its content inside the app (nothing is
    /// downloaded), so "resources" here means what the game writes on the device:
    /// save files (persistentDataPath) and throw-away cache (temporaryCachePath + Unity's cache).
    /// Save files are only ever read here — cleanup never touches them.
    /// </summary>
    public static class StorageTools
    {
        public static long SaveDataBytes()
        {
            long total = 0;
            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
            {
                total += FileBytes(Path.Combine(Application.persistentDataPath, GameConstants.Save.GetSaveFileName(i)));
                total += FileBytes(Path.Combine(Application.persistentDataPath, GameConstants.Save.GetBackupFileName(i)));
            }
            return total;
        }

        public static int SaveSlotsUsed()
        {
            int used = 0;
            for (int i = 0; i < SaveManager.MAX_SLOTS; i++)
                if (File.Exists(Path.Combine(Application.persistentDataPath, GameConstants.Save.GetSaveFileName(i)))) used++;
            return used;
        }

        public static long CacheBytes()
        {
            long total = DirectoryBytes(Application.temporaryCachePath);
            if (Caching.ready) total += Caching.defaultCache.spaceOccupied;
            return total;
        }

        /// <summary>Deletes cached files (never save data). Returns bytes freed.</summary>
        public static long ClearCache()
        {
            long before = CacheBytes();
#if !UNITY_EDITOR
            // In the editor this folder is shared with Unity itself, so only clear it on devices.
            try
            {
                var dir = new DirectoryInfo(Application.temporaryCachePath);
                if (dir.Exists)
                {
                    foreach (FileInfo f in dir.GetFiles("*", SearchOption.AllDirectories))
                    {
                        try { f.Delete(); } catch (Exception) { /* in use — skip */ }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[StorageTools] Cache cleanup skipped: {e.Message}"); }
#endif
            if (Caching.ready) Caching.ClearCache();
            return Math.Max(0, before - CacheBytes());
        }

        /// <summary>Quick Cleanup: clear the cache and release memory the current scene no longer uses.</summary>
        public static long QuickCleanup()
        {
            long freed = ClearCache();
            Resources.UnloadUnusedAssets();
            GC.Collect();
            return freed;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024f:0.#} KB";
            return $"{bytes / (1024f * 1024f):0.#} MB";
        }

        private static long FileBytes(string path)
        {
            try { return File.Exists(path) ? new FileInfo(path).Length : 0; }
            catch (Exception) { return 0; }
        }

        private static long DirectoryBytes(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                if (!dir.Exists) return 0;
                long total = 0;
                foreach (FileInfo f in dir.GetFiles("*", SearchOption.AllDirectories)) total += f.Length;
                return total;
            }
            catch (Exception) { return 0; }
        }
    }
}
