
using UnityEngine;

[CreateAssetMenu(fileName = "PhaseSettings", menuName = "Game/Phase Settings")]
public class PhaseSettings : ScriptableObject
{
    [SerializeField] private float _dayDuration = 90f;
    [SerializeField] private int _totalDays = 7;

    public float DayDuration => _dayDuration;
    public int TotalDays => _totalDays;
}