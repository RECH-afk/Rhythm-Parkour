using UnityEngine;

/// <summary>
/// Простой DontDestroyOnLoad перенос уровня между сценами.
/// Держит LevelTransfer данные живыми при загрузке сцен.
/// Вешай на пустой GO в первой сцене или создается авто.
/// </summary>
public class LevelTransferBehaviour : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        // не уничтожать при загрузке новой сцены
        if (FindObjectsOfType<LevelTransferBehaviour>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        Debug.Log("[LevelTransfer] DontDestroyOnLoad создан", this);
    }
}
