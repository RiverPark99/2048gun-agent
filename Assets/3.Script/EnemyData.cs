using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "2048Gun/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [System.Serializable]
    public class StageData
    {
        public string stageName;
        public int hp = 200;
        public int atk = 20;
        public int turnInterval = 8;
    }

    [Header("Stage 1~9 (Tutorial)")]
    public StageData[] tutorialStages = new StageData[9];

    [Header("Stage 10~39 (Normal)")]
    public StageData[] normalStages = new StageData[30];

    [Header("Stage 40 (Guard Boss)")]
    public StageData guardBoss = new StageData();

    [Header("Stage 41+ (Clear Mode Loop)")]
    public StageData clearModeEnemy = new StageData();

    public StageData GetStageData(int bossLevel)
    {
        if (bossLevel >= 1 && bossLevel <= 9)
        {
            int idx = bossLevel - 1;
            if (idx < tutorialStages.Length && tutorialStages[idx] != null)
                return tutorialStages[idx];
        }
        else if (bossLevel >= 10 && bossLevel <= 39)
        {
            int idx = bossLevel - 10;
            if (idx < normalStages.Length && normalStages[idx] != null)
                return normalStages[idx];
        }
        else if (bossLevel == 40)
        {
            return guardBoss;
        }
        else if (bossLevel >= 41)
        {
            return clearModeEnemy;
        }

        return new StageData { hp = 200, atk = 20, turnInterval = 8 };
    }

    void OnValidate()
    {
        if (tutorialStages == null || tutorialStages.Length != 9)
            tutorialStages = new StageData[9];
        if (normalStages == null || normalStages.Length != 30)
            normalStages = new StageData[30];

        for (int i = 0; i < tutorialStages.Length; i++)
        {
            if (tutorialStages[i] == null) tutorialStages[i] = new StageData();
            if (string.IsNullOrEmpty(tutorialStages[i].stageName))
                tutorialStages[i].stageName = $"Stage {i + 1}";
        }
        for (int i = 0; i < normalStages.Length; i++)
        {
            if (normalStages[i] == null) normalStages[i] = new StageData();
            if (string.IsNullOrEmpty(normalStages[i].stageName))
                normalStages[i].stageName = $"Stage {i + 10}";
        }
        if (guardBoss == null) guardBoss = new StageData();
        if (string.IsNullOrEmpty(guardBoss.stageName)) guardBoss.stageName = "Guard Boss (40)";
        if (clearModeEnemy == null) clearModeEnemy = new StageData();
        if (string.IsNullOrEmpty(clearModeEnemy.stageName)) clearModeEnemy.stageName = "Clear Mode (41+)";
    }
}
