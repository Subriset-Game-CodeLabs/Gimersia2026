using TMPro;
using UnityEngine;

public class ResultHud : MonoBehaviour
{
    [SerializeField] private WinLoseChecker _checker;
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _label;

    private void Awake()
    {
        _panel.SetActive(false);
        _checker.OnResult += HandleResult;
    }

    private void OnDestroy()
    {
        if (_checker != null) _checker.OnResult -= HandleResult;
    }

    private void HandleResult(RunResult result)
    {
        _label.text = result == RunResult.Won ? "YOU WIN" : "YOU LOSE";
        _panel.SetActive(true);
    }
}
