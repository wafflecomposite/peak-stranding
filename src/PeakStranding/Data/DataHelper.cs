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
}