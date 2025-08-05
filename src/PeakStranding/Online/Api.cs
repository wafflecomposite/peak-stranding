using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static string _cachedSteamAuthTicket = string.Empty;

        private static readonly HttpClient http = new()
        {
            Timeout = TimeSpan.FromSeconds(9)
        };

        private static readonly string DefaultBaseUrl = $"http://127.0.0.1:3000/api/v1";
        //private static readonly string DefaultBaseUrl = $"https://peakstranding.burning.homes/api/v1";

        internal static string GetBaseUrl()
        {
            return string.IsNullOrEmpty(Plugin.CfgRemoteApiUrl)
                ? DefaultBaseUrl
                : Plugin.CfgRemoteApiUrl.TrimEnd('/');
        }
        internal static string StructuresUrl => $"{GetBaseUrl()}/structures";

        private static string GetAuthTicket()
        {
            if (string.IsNullOrEmpty(_cachedSteamAuthTicket))
            {
                _cachedSteamAuthTicket = SteamAuthTicketService.GetSteamAuthTicket().Item1;
            }
            return _cachedSteamAuthTicket;
        }

        public static void Upload(int mapId, PlacedItemData item)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var body = JsonConvert.SerializeObject(item.ToServerDto(SteamUser.GetSteamID().m_SteamID, SteamFriends.GetPersonaName(), mapId));
                    Plugin.Log.LogInfo($"Uploading item to remote");
                    //Plugin.Log.LogInfo($"Uploading item to remote: {body}");
                    using var req = new HttpRequestMessage(HttpMethod.Post, StructuresUrl)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json")
                    };
                    req.Headers.Add("X-Steam-Auth", GetAuthTicket());
                    var resp = await http.SendAsync(req).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogInfo($"Remote upload failed: {ex.Message}");
                }
            });
        }


        public static async Task<List<ServerStructureDto>> FetchStructuresAsync(int mapId)
        {
            var scene = Uri.EscapeDataString(DataHelper.GetCurrentSceneName());
            //var mapId = DataHelper.GetCurrentLevelIndex();

            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var url = $"{StructuresUrl}?scene={scene}&map_id={mapId}&limit={Plugin.CfgRemoteStructuresLimit}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Steam-Auth", GetAuthTicket());
                using var resp = await http.SendAsync(req).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(json))
                {
                    Plugin.Log.LogInfo($"Remote fetch returned no items for map {mapId}");
                    return new List<ServerStructureDto>();
                }
                var dtoList = JsonConvert.DeserializeObject<List<ServerStructureDto>>(json);
                if (dtoList == null || dtoList.Count == 0)
                {
                    Plugin.Log.LogInfo($"Remote fetch returned no items for map {mapId}");
                    return new List<ServerStructureDto>();
                }

                var uniqueUsers = new HashSet<ulong>();
                foreach (var dto in dtoList)
                {
                    uniqueUsers.Add(dto.user_id);
                }
                stopwatch.Stop();
                Plugin.Log.LogInfo($"Received {dtoList.Count} online structures from {uniqueUsers.Count} users for map_id {mapId}, took {stopwatch.ElapsedMilliseconds} ms");
                //UIHandler.Instance.Toast($"Received {dtoList.Count} online structures from {uniqueUsers.Count} users ({stopwatch.ElapsedMilliseconds} ms)", Color.green, 5f, 3f);

                return dtoList;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogInfo($"Remote fetch failed: {ex.Message}");
                return new List<ServerStructureDto>();
            }
        }

    }
}