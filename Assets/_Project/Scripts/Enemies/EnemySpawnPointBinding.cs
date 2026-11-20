using System;
using UnityEngine;

[Serializable]
public class EnemySpawnPointBinding
{
    [SerializeField] private string _id;
    [SerializeField] private Transform _point;

    public string Id => _id;
    public Transform Point => _point;
}
