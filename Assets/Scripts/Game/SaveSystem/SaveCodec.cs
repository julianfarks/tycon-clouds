using System;
using UnityEngine;

namespace CloudGarden.SaveSystem
{
    /// <summary>Encodes/decodes saves as JSON with an integrity hash.</summary>
    public static class SaveCodec
    {
        public static string Encode(SaveData data)
        {
            string payload = JsonUtility.ToJson(data);
            SaveEnvelope env = new SaveEnvelope();
            env.Version = data.Version;
            env.Payload = payload;
            env.Hash = Hash(payload);
            return JsonUtility.ToJson(env);
        }

        public static bool TryDecode(string text, out SaveData data)
        {
            data = null;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                SaveEnvelope env = JsonUtility.FromJson<SaveEnvelope>(text);
                if (env == null || string.IsNullOrEmpty(env.Payload) || string.IsNullOrEmpty(env.Hash)) return false;
                if (Hash(env.Payload) != env.Hash) return false;
                SaveData d = JsonUtility.FromJson<SaveData>(env.Payload);
                if (!SaveMapper.Validate(d)) return false;
                data = d;
                return true;
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        /// <summary>FNV-1a 64-bit hash as hexadecimal string.</summary>
        public static string Hash(string s)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }
    }
}
