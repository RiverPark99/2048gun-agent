using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossManager : MonoBehaviour
{
    [Header("Boss UI References")]
    public Image bossImageArea;
    public Image hpBarFill;
    public TextMeshProUGUI hpText;
    
    [Header("Boss Stats")]
    public int maxHP = 256;
    private int currentHP;
    
    [Header("Boss Progression")]
    public int bossLevel = 1;
    public float hpMultiplier = 1.41421356f; // √2 (sqrt(2))
    
    void Start()
    {
        InitializeBoss();
    }
    
    void InitializeBoss()
    {
        maxHP = Mathf.RoundToInt(256 * Mathf.Pow(hpMultiplier, bossLevel - 1));
        currentHP = maxHP;
        UpdateUI();
        Debug.Log($"Boss Level {bossLevel} spawned! HP: {currentHP}/{maxHP}");
    }
    
    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        
        if (currentHP <= 0)
        {
            currentHP = 0;
            OnBossDefeated();
        }
        
        Debug.Log($"Boss took {damage} damage! Current HP: {currentHP}/{maxHP}");
        UpdateUI();
    }
    
    void UpdateUI()
    {
        if (hpBarFill != null)
        {
            float fillPercentage = (float)currentHP / (float)maxHP;
            hpBarFill.fillAmount = fillPercentage;
            Debug.Log($"HP Bar updated! fillAmount: {fillPercentage} ({currentHP}/{maxHP})");
        }
        else
        {
            Debug.LogWarning("hpBarFill is NULL! Please connect it in Inspector.");
        }
        
        if (hpText != null)
        {
            hpText.text = "HP: " + currentHP + " / " + maxHP;
        }
        else
        {
            Debug.LogWarning("hpText is NULL! Please connect it in Inspector.");
        }
    }
    
    void OnBossDefeated()
    {
        Debug.Log("Boss " + bossLevel + " defeated!");
        bossLevel++;
        InitializeBoss();
    }
    
    public int GetCurrentHP() { return currentHP; }
    public int GetMaxHP() { return maxHP; }
    public int GetBossLevel() { return bossLevel; }
}
