using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PeakStranding.Data;
using Steamworks;
using UnityEngine;

namespace PeakStranding.Online
{
    internal static class RemoteApi
    {
        private static readonly HttpClient http = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        private const string BaseUrl = "http://127.0.0.1:8090/api/collections/structures/records";

        // ---------- upload ----------

        public static void UploadOnce(int mapId, PlacedItemData item)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var body = JsonConvert.SerializeObject(new
                    {
                        //id = Guid.NewGuid(),
                        user_id = SteamUser.GetSteamID().m_SteamID,
                        map_id = mapId,
                        prefab = item.PrefabName,
                        pos_x = item.Position.x,
                        pos_y = item.Position.y,
                        pos_z = item.Position.z,
                        rot_x = item.Rotation.x,
                        rot_y = item.Rotation.y,
                        rot_z = item.Rotation.z,
                        rot_w = item.Rotation.w,
                        cell_x = Mathf.FloorToInt(item.Position.x / 50f),
                        cell_z = Mathf.FloorToInt(item.Position.z / 50f)
                    });

                    using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    // No auth header for the PoC
                    var resp = await http.SendAsync(req).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogInfo($"Remote upload failed: {ex.Message}");
                }
            });
        }

        // ---------- download ----------

        public static async Task<List<PlacedItemData>> FetchAmbientAsync(int mapId)
        {
            try
            {
                //var url = $"{BaseUrl}?filter=(map_id={mapId}&&user_id!=\"{SteamUser.GetSteamID().m_SteamID}\")" +
                //"&perPage=50&sort=-created&_=" + UnityEngine.Random.Range(0, int.MaxValue);
                var url = $"{BaseUrl}?filter=(map_id={mapId})&perPage=5&sort=@random";
                using var resp = await http.GetAsync(url).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                var serverData = JsonConvert.DeserializeObject<PbListResponse<ServerStructure>>(json);
                if (serverData == null || serverData.items == null)
                {
                    Plugin.Log.LogInfo($"Remote fetch returned no items for map {mapId}");
                    return new List<PlacedItemData>();
                }

                var list = new List<PlacedItemData>(serverData.items.Count);

                foreach (ServerStructure s in serverData.items)
                {
                    list.Add(new PlacedItemData
                    {
                        PrefabName = s.prefab,
                        Position = new Vector3(s.pos_x, s.pos_y, s.pos_z),
                        Rotation = new Quaternion(s.rot_x, s.rot_y, s.rot_z, s.rot_w)
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"Remote fetch failed: {ex.Message}");
                return new List<PlacedItemData>();   // fall back to offline only
            }
        }

        // PocketBase list wrapper
        private class PbListResponse<T>
        {
            public List<T> items;
        }
    }

    class ServerStructure
    {
        public string prefab;
        public float pos_x, pos_y, pos_z;
        public float rot_x, rot_y, rot_z, rot_w;
        public int cell_x, cell_z;
    }
}