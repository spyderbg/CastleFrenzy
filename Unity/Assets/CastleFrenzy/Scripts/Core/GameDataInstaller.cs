// Android keeps StreamingAssets inside the APK, where regular file IO cannot reach it. On first launch (and after
// an update) the GameData folder is copied to persistent storage using the manifest written at build time, and
// GameFiles.Root is pointed there so every loader keeps using plain files.
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace CastleFrenzy
{
    public static class GameDataInstaller
    {
        public const string ManifestName = "manifest.txt";

        public static void EnsureInstalled()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            string target = Path.Combine(Application.persistentDataPath, "GameData").Replace('\\', '/');
            string manifest = Fetch(Application.streamingAssetsPath + "/GameData/" + ManifestName);
            if (manifest == null) { Debug.LogError("[CastleFrenzy] GameData manifest missing from the APK"); return; }
            string stamp = Application.version + "\n" + manifest.GetHashCode();
            string stampPath = target + "/.installed";
            if (!File.Exists(stampPath) || File.ReadAllText(stampPath) != stamp)
            {
                int n = 0;
                foreach (string line in manifest.Split('\n'))
                {
                    string rel = line.Trim();
                    if (rel.Length == 0 || rel.StartsWith("#")) continue;
                    byte[] data = FetchBytes(Application.streamingAssetsPath + "/GameData/" + rel);
                    if (data == null) { Debug.LogError("[CastleFrenzy] Could not extract " + rel); continue; }
                    string dst = target + "/" + rel;
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.WriteAllBytes(dst, data);
                    n++;
                }
                File.WriteAllText(stampPath, stamp);
                Debug.Log("[CastleFrenzy] Installed " + n + " game data files to " + target);
            }
            GameFiles.Root = target;
        }

        static byte[] FetchBytes(string url)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (!op.isDone) { }
                return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.data : null;
            }
        }

        static string Fetch(string url)
        {
            byte[] b = FetchBytes(url);
            return b != null ? System.Text.Encoding.UTF8.GetString(b) : null;
        }
    }
}
