using System;
using System.Collections.Generic;
using System.Linq;

namespace PeakStranding.Data;

public static class DataHelper
{
    public static string GetCurrentSceneName() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    public static int GetCurrentMapSegment()
    {
        if (MapHandler.Instance == null)
        {
            return -1;
        }
        return (int)MapHandler.Instance.GetCurrentSegment();
    }

    public static int GetCurrentLevelIndex() => GameHandler.GetService<NextLevelService>().Data.Value.CurrentLevelIndex;

    public static BiDictionary prefabMapping = new BiDictionary()
    {
        { "0_Items/BounceShroomSpawn", "bounceshroom" },
        { "0_Items/ClimbingSpikeHammered", "piton" },
        { "0_Items/ShelfShroomSpawn", "shelfshroom" },
        { "Flag_planted_seagull", "flagseagull" },
        { "Flag_planted_turtle", "flagturtle" },
        { "PeakStranding/JungleVine", "chainshooter" },
        { "PeakStranding/MagicBeanVine", "magicbean" },
        { "PeakStranding/RopeShooter", "ropeshooter" },
        { "PeakStranding/RopeSpool", "ropespool" },
        { "PortableStovetop_Placed", "stove" },
        { "ScoutCannon_Placed", "scoutcannon" },
    };

    public static bool IsPrefabAllowed(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName)) return false;
        prefabMapping.TryGetByFirst(prefabName, out string readablePrefabName);
        if (string.IsNullOrWhiteSpace(readablePrefabName)) return false;
        if (string.IsNullOrWhiteSpace(Plugin.CfgStructureAllowList)) return true;

        return Plugin.CfgStructureAllowList.ToLower().Contains(readablePrefabName.ToLower());
    }

    public static List<string> GetExcludedPrefabs()
    {
        var excludedInternalNames = new List<string>();

        if (string.IsNullOrWhiteSpace(Plugin.CfgStructureAllowList))
        {
            return excludedInternalNames;
        }

        var allowedReadableNames = new HashSet<string>(
            Plugin.CfgStructureAllowList.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
        );

        foreach (var mapping in prefabMapping)
        {
            var internalName = mapping.Key;
            var readableName = mapping.Value;
            if (!allowedReadableNames.Contains(readableName.ToLower()))
            {
                excludedInternalNames.Add(internalName);
            }
        }

        return excludedInternalNames;
    }

}