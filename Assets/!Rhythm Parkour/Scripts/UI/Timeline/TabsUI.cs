using UnityEngine;
using UnityEngine.UI;
public class TabsUI : MonoBehaviour
{
    public GameObject[] panels;
    public Button[] buttons;
    public int defaultIndex = 0;
    int current = -1;
    void Start()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int idx = i;
            if (buttons[i] != null) buttons[i].onClick.AddListener(() => Show(idx));
        }
        Show(defaultIndex);
    }
    public void Show(int index)
    {
        if (panels == null) return;
        for (int i = 0; i < panels.Length; i++) if (panels[i] != null) panels[i].SetActive(i == index);
        for (int i = 0; i < buttons.Length; i++) if (buttons[i] != null) buttons[i].interactable = i != index;
        current = index;
    }
    public void ShowByName(string name)
    {
        if (panels == null) return;
        for (int i = 0; i < panels.Length; i++) if (panels[i] != null && panels[i].name == name) { Show(i); return; }
    }
}
