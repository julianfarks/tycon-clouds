using System;
using System.IO;
using UnityEngine;

namespace CloudGarden.SaveSystem
{
    public enum LoadSource { None = 0, Main, Backup1, Backup2 }

    /// <summary>
    /// Persistent storage with atomic writes and two rolling backups. If the main file is corrupt the
    /// newest valid backup is used.
    /// </summary>
    public sealed class SaveManager
    {
        readonly string mainPath;
        readonly string tmpPath;
        readonly string bak1;
        readonly string bak2;

        public SaveManager(string directory, string fileName)
        {
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            mainPath = Path.Combine(directory, fileName);
            tmpPath = mainPath + ".tmp";
            bak1 = mainPath + ".bak1";
            bak2 = mainPath + ".bak2";
        }

        public static SaveManager CreateDefault()
        {
            return new SaveManager(Application.persistentDataPath, "cloudgarden_save.json");
        }

        public string MainPath { get { return mainPath; } }
        public string Backup1Path { get { return bak1; } }
        public string Backup2Path { get { return bak2; } }

        public bool Save(SaveData data)
        {
            try
            {
                string text = SaveCodec.Encode(data);
                File.WriteAllText(tmpPath, text);

                // Verify the written file before replacing anything.
                SaveData check;
                if (!SaveCodec.TryDecode(File.ReadAllText(tmpPath), out check))
                {
                    File.Delete(tmpPath);
                    return false;
                }

                if (File.Exists(mainPath))
                {
                    // Only rotate backups when the current main file is itself valid.
                    SaveData current;
                    if (SaveCodec.TryDecode(File.ReadAllText(mainPath), out current))
                    {
                        if (File.Exists(bak1)) File.Copy(bak1, bak2, true);
                        File.Copy(mainPath, bak1, true);
                    }
                    File.Delete(mainPath);
                }
                File.Move(tmpPath, mainPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CloudGarden] Save failed: " + e.Message);
                return false;
            }
        }

        public SaveData Load(out LoadSource source)
        {
            source = LoadSource.None;
            SaveData d;
            if (TryRead(mainPath, out d)) { source = LoadSource.Main; return d; }
            if (File.Exists(mainPath))
            {
                try { File.Copy(mainPath, mainPath + ".corrupt", true); } catch (Exception) { }
            }
            if (TryRead(bak1, out d)) { source = LoadSource.Backup1; return d; }
            if (TryRead(bak2, out d)) { source = LoadSource.Backup2; return d; }
            return null;
        }

        static bool TryRead(string path, out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                return SaveCodec.TryDecode(File.ReadAllText(path), out data);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void DeleteAll()
        {
            string[] paths = { mainPath, tmpPath, bak1, bak2, mainPath + ".corrupt" };
            for (int i = 0; i < paths.Length; i++)
            {
                try { if (File.Exists(paths[i])) File.Delete(paths[i]); } catch (Exception) { }
            }
        }
    }
}
