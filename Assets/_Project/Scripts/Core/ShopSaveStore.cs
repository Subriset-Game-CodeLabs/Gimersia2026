using System;
using System.IO;
using System.Text;
using UnityEngine;
public sealed class ShopSaveStore : IShopSaveStore
{
    private readonly string _path;
    public ShopSaveStore(string path) => _path = path;

    public bool TryLoad(out ShopSaveData data, out bool exists, out string error)
    {
        data = null;
        error = null;
        exists = File.Exists(_path) || File.Exists(_path + ".bak") || File.Exists(_path + ".tmp");
        if (!exists) return true;
        // A leftover temp is not a committed transaction. Never use it as player state.
        if (TryRead(_path, out data)) return true;
        if (TryRead(_path + ".bak", out data))
        {
            error = "Primary shop save unavailable; recovered the last backup. Original files are retained until a successful save.";
            return true;
        }
        error = "Shop save is invalid or unreadable (including backup). No state was reset. Restore a backup or explicitly archive the files before creating a new save.";
        return false;
    }

    private static bool TryRead(string path, out ShopSaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return false;
            string json = File.ReadAllText(path, Encoding.UTF8);
            // JsonUtility supplies field defaults for missing fields; require the document envelope.
            if (!json.Contains("\"version\"") || !json.Contains("\"coins\"") || !json.Contains("\"items\"")) return false;
            data = JsonUtility.FromJson<ShopSaveData>(json);
            return data != null && data.IsValid();
        }
        catch (Exception) { return false; }
    }

    public bool TrySave(ShopSaveData data, out string error)
    {
        error = null;
        if (data == null || !data.IsValid()) { error = "Invalid shop state; save refused."; return false; }
        try
        {
            string directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            using (var stream = new FileStream(_path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(_path))
            {
                // Do not replace a good backup with a corrupt primary during recovery.
                if (TryRead(_path, out _)) File.Replace(_path + ".tmp", _path, _path + ".bak");
                else File.Replace(_path + ".tmp", _path, null);
            }
            else File.Move(_path + ".tmp", _path);
            return true;
        }
        catch (Exception exception)
        {
            error = "Shop save failed: " + exception.Message;
            return false;
        }
    }
}
