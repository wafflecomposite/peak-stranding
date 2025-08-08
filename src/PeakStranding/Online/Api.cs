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
            Timeout = TimeSpan.FromSeconds(10)
        };

        //private static readonly string DefaultBaseUrl = $"http://127.0.0.1:3000/api/v1";
        private static readonly string DefaultBaseUrl = $"https://peakstranding.burning.homes/api/v1";

        internal static string GetBaseUrl()
        {
            return string.IsNullOrEmpty(Plugin.CfgRemoteApiUrl)
                ? DefaultBaseUrl
                : Plugin.CfgRemoteApiUrl.TrimEnd('/');
        }
        internal static string StructuresUrl => $"{GetBaseUrl()}/structures";

        private static string GetAuthTicket(bool forceRefresh = false)
        {
            if (forceRefresh || string.IsNullOrEmpty(_cachedSteamAuthTicket))
            {
                _cachedSteamAuthTicket = SteamAuthTicketService.GetSteamAuthTicket().Item1;
            }
            Plugin.Log.LogInfo($"Using auth ticket: {_cachedSteamAuthTicket}");
            return _cachedSteamAuthTicket;
        }

        private static void InvalidateAuthTicket()
        {
            _cachedSteamAuthTicket = string.Empty;
        }

        public static void Upload(int mapId, PlacedItemData item)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteWithAuthRetry(async (authTicket) =>
                    {
                        var body = JsonConvert.SerializeObject(item.ToServerDto(SteamUser.GetSteamID().m_SteamID, SteamFriends.GetPersonaName(), mapId));
                        Plugin.Log.LogInfo($"Uploading item to remote");
                        using var req = new HttpRequestMessage(HttpMethod.Post, StructuresUrl)
                        {
                            Content = new StringContent(body, Encoding.UTF8, "application/json")
                        };
                        req.Headers.Add("X-Steam-Auth", authTicket);
                        var resp = await http.SendAsync(req).ConfigureAwait(false);

                        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                            resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            throw new HttpRequestException($"Authentication failed with status code: {resp.StatusCode}");
                        }

                        resp.EnsureSuccessStatusCode();
                        return true;
                    });
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogInfo($"Remote upload failed: {ex.Message}");
                }
                return Task.CompletedTask;
            });
        }


        public static async Task<List<ServerStructureDto>> FetchStructuresAsync(int mapId)
        {
            return await ExecuteWithAuthRetry(async (authTicket) =>
            {
                var scene = Uri.EscapeDataString(DataHelper.GetCurrentSceneName());
                var urlBuilder = new StringBuilder($"{StructuresUrl}?scene={scene}&map_id={mapId}&limit={Plugin.CfgRemoteStructuresLimit}");

                var excludedPrefabs = DataHelper.GetExcludedPrefabs();
                if (excludedPrefabs.Count > 0)
                {
                    // Join the list into a comma-separated string, URL-encode it, and append it to the URL
                    // The server will receive a param like &exclude_prefabs=PrefabName1,PrefabName2
                    var excludedParam = Uri.EscapeDataString(string.Join(",", excludedPrefabs));
                    urlBuilder.Append($"&exclude_prefabs={excludedParam}");
                }
                var url = urlBuilder.ToString();
                Plugin.Log.LogInfo($"Fetching structures from remote: {url}");
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("X-Steam-Auth", authTicket);
                using var resp = await http.SendAsync(req).ConfigureAwait(false);

                if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new HttpRequestException($"Authentication failed with status code: {resp.StatusCode}");
                }

                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var dtoList = JsonConvert.DeserializeObject<List<ServerStructureDto>>(content) ?? new List<ServerStructureDto>();

                // Track unique users for logging
                var uniqueUsers = new HashSet<ulong>();
                foreach (var dto in dtoList)
                {
                    uniqueUsers.Add(dto.user_id);
                }
                Plugin.Log.LogInfo($"Received {dtoList.Count} online structures from {uniqueUsers.Count} users for map_id {mapId}");
                return dtoList;
            });
        }

        private static async Task<T> ExecuteWithAuthRetry<T>(Func<string, Task<T>> action, int maxRetries = 1)
        {
            int attempt = 0;
            bool shouldRetry;
            Exception lastException;

            do
            {
                shouldRetry = false;
                try
                {
                    var authTicket = GetAuthTicket(attempt > 0); // Force refresh on retry
                    return await action(authTicket).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        Plugin.Log.LogInfo($"Request failed with {ex.Message}, refreshing auth ticket and retrying...");
                        InvalidateAuthTicket();
                        shouldRetry = true;
                        attempt++;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        shouldRetry = true;
                        attempt++;
                    }
                }
            } while (shouldRetry);

            Plugin.Log.LogInfo($"Request failed after {attempt} attempts: {lastException?.Message}");
            if (lastException != null)
            {
                throw lastException;
            }
            return default;
        }
    }
}