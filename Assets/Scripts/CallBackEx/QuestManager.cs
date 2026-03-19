using UnityEngine;

public class QuestManager : MonoBehaviour, IQuestCallbacks
{
    [SerializeField] private Monster monster;
    private int KillCount = 0;
    void Start()
    {
        monster.callbacks = this;
    }

    public void OnMonsterKilled(string monsterName)
    {
        KillCount++;
        Debug.Log($"{monsterName} 처치 수 : {KillCount}");

        if (KillCount > 0)
        {
            Debug.Log("퀘스트 완료");
        }
    }
}

