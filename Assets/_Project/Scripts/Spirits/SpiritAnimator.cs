using System;
using UnityEngine;

[Serializable]
public struct WeightedAnimState
{
    public string stateName;
    [Min(1f)] public float weight;
}

public class SpiritAnimator : MonoBehaviour
{
    [Header("Animator Reference")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackSpeedParam = "AttackSpeed";
    [SerializeField] private float _crossFadeDuration = 0.12f;
    [SerializeField, Min(0.05f)] private float _minMoveVisualHold = 0.16f;

    [Header("1. Idle Utama (3 Animasi - 1 Lebih Sering)")]
    [SerializeField] private WeightedAnimState[] _mainIdleStates =
    {
        new WeightedAnimState { stateName = "Idle_1", weight = 60f },
        new WeightedAnimState { stateName = "Idle_2", weight = 20f },
        new WeightedAnimState { stateName = "Idle_3", weight = 20f }
    };

    [Header("2. Idle Tambahan Setelah 20 Detik (2 Animasi - 1 Lebih Sering)")]
    [SerializeField] private float _longIdleDelay = 20f;
    [SerializeField] private WeightedAnimState[] _longIdleStates =
    {
        new WeightedAnimState { stateName = "LongIdle_1", weight = 70f },
        new WeightedAnimState { stateName = "LongIdle_2", weight = 30f }
    };

    [Header("3. Attack (3 Normal + 1 Special Attack)")]
    [SerializeField] private WeightedAnimState[] _normalAttackStates =
    {
        new WeightedAnimState { stateName = "Attack_1", weight = 30f },
        new WeightedAnimState { stateName = "Attack_2", weight = 30f },
        new WeightedAnimState { stateName = "Attack_3", weight = 25f }
    };
    [SerializeField] private WeightedAnimState _specialAttackState =
        new WeightedAnimState { stateName = "SpecialAttack", weight = 15f };

    [Header("4. Pohon / Work (4 Animasi Acak)")]
    [SerializeField] private WeightedAnimState[] _treeWorkStates =
    {
        new WeightedAnimState { stateName = "Work_1", weight = 25f },
        new WeightedAnimState { stateName = "Work_2", weight = 25f },
        new WeightedAnimState { stateName = "Work_3", weight = 25f },
        new WeightedAnimState { stateName = "Work_4", weight = 25f }
    };

    [Header("5. Move (1 Animasi)")]
    [SerializeField] private string _moveStateName = "Move";

    private SpiritController _controller;
    private float _inactivityTimer;
    private float _moveVisualStartedAt;
    private bool _isPlayingLongIdle;
    private bool _isMovingVisual;
    private bool _isAttackPlaying;
    private string _currentStateName;

    public bool IsAttackInProgress => _isAttackPlaying;

    private void Awake()
    {
        _controller = GetComponent<SpiritController>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (_controller != null)
            _controller.OnStateChanged += HandleSpiritStateChanged;
    }

    private void OnDisable()
    {
        if (_controller != null)
            _controller.OnStateChanged -= HandleSpiritStateChanged;
    }

    private void Start()
    {
        PlayRandomIdle(forceLongIdle: false);
    }

    public void NotifyPlayerInteraction()
    {
        _inactivityTimer = 0f;

        if (_isPlayingLongIdle && (_controller == null || _controller.State == SpiritState.Idle))
        {
            _isPlayingLongIdle = false;
            PlayRandomIdle(forceLongIdle: false);
        }
    }

    private void HandleSpiritStateChanged(SpiritState newState)
    {
        _isAttackPlaying = false;

        switch (newState)
        {
            case SpiritState.Idle:
                _isMovingVisual = false;
                PlayRandomIdle(_inactivityTimer >= _longIdleDelay);
                break;

            case SpiritState.Moving:
                _isMovingVisual = true;
                _moveVisualStartedAt = Time.time;
                CrossFadeTo(_moveStateName);
                break;

            case SpiritState.Interacting:
                _isMovingVisual = false;
                PlayRandomWork();
                break;

            case SpiritState.Fighting:
                if (_controller != null && _controller.IsChasingEnemy)
                {
                    _isMovingVisual = true;
                    _moveVisualStartedAt = Time.time;
                    CrossFadeTo(_moveStateName);
                }
                else
                {
                    _isMovingVisual = false;
                    PlayRandomIdle(forceLongIdle: false);
                }
                break;
        }
    }

    private void Update()
    {
        if (_animator == null || _controller == null) return;

        SpiritState state = _controller.State;

        if (state == SpiritState.Idle)
        {
            _inactivityTimer += Time.deltaTime;
            bool shouldBeLongIdle = _inactivityTimer >= _longIdleDelay;

            if (shouldBeLongIdle && !_isPlayingLongIdle)
            {
                _isPlayingLongIdle = true;
                PlayRandomIdle(forceLongIdle: true);
                return;
            }

            if (HasCurrentClipFinished(ignoreLoopFlag: false))
            {
                PlayRandomIdle(_isPlayingLongIdle);
            }
        }
        else if (state == SpiritState.Interacting)
        {
            if (HasCurrentClipFinished(ignoreLoopFlag: false))
            {
                PlayRandomWork();
            }
        }
        else if (state == SpiritState.Fighting)
        {
            bool chasing = _controller.IsChasingEnemy;
            if (chasing && !_isMovingVisual)
            {
                _isMovingVisual = true;
                _moveVisualStartedAt = Time.time;
                _isAttackPlaying = false;
                CrossFadeTo(_moveStateName);
            }
            else if (!chasing && _isMovingVisual)
            {
                if (Time.time - _moveVisualStartedAt >= _minMoveVisualHold)
                {
                    _isMovingVisual = false;
                    PlayRandomIdle(forceLongIdle: false);
                }
            }
            else if (!chasing && _isAttackPlaying && HasCurrentClipFinished(ignoreLoopFlag: true))
            {
                _isAttackPlaying = false;
                PlayRandomIdle(forceLongIdle: false);
            }
        }
    }

    public bool PlayAttack(float attackSpeedMultiplier)
    {
        _inactivityTimer = 0f;
        _isPlayingLongIdle = false;
        _isMovingVisual = false;
        _isAttackPlaying = true;

        if (_animator != null && !string.IsNullOrEmpty(_attackSpeedParam))
        {
            _animator.SetFloat(_attackSpeedParam, Mathf.Max(0.5f, attackSpeedMultiplier));
        }

        float totalNormalWeight = 0f;
        for (int i = 0; i < _normalAttackStates.Length; i++)
            totalNormalWeight += Mathf.Max(0f, _normalAttackStates[i].weight);

        float specialWeight = Mathf.Max(0f, _specialAttackState.weight);
        float totalWeight = totalNormalWeight + specialWeight;

        if (totalWeight <= 0f) return false;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        if (roll < specialWeight)
        {
            CrossFadeTo(_specialAttackState.stateName, restartIfSame: true);
            return true;
        }

        string chosenNormal = PickWeighted(_normalAttackStates);
        CrossFadeTo(chosenNormal, restartIfSame: true);
        return false;
    }

    private void PlayRandomIdle(bool forceLongIdle)
    {
        WeightedAnimState[] pool = (forceLongIdle && _longIdleStates != null && _longIdleStates.Length > 0)
            ? _longIdleStates
            : _mainIdleStates;

        string chosen = PickWeighted(pool);
        CrossFadeTo(chosen, restartIfSame: true);
    }

    private void PlayRandomWork()
    {
        string chosen = PickWeighted(_treeWorkStates);
        CrossFadeTo(chosen, restartIfSame: true);
    }

    private static string PickWeighted(WeightedAnimState[] pool)
    {
        if (pool == null || pool.Length == 0) return string.Empty;

        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
            total += Mathf.Max(0f, pool[i].weight);

        if (total <= 0f) return pool[0].stateName;

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            cumulative += Mathf.Max(0f, pool[i].weight);
            if (roll <= cumulative)
                return pool[i].stateName;
        }

        return pool[pool.Length - 1].stateName;
    }

    private void CrossFadeTo(string stateName, bool restartIfSame = false)
    {
        if (_animator == null || string.IsNullOrEmpty(stateName)) return;
        if (!restartIfSame && _currentStateName == stateName) return;

        _currentStateName = stateName;
        _animator.CrossFadeInFixedTime(stateName, _crossFadeDuration, 0, 0f);
    }

    private bool HasCurrentClipFinished(bool ignoreLoopFlag)
    {
        if (_animator == null || _animator.IsInTransition(0)) return false;
        AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
        if (!string.IsNullOrEmpty(_currentStateName) && !info.IsName(_currentStateName))
            return false;

        return (ignoreLoopFlag || !info.loop) && info.normalizedTime >= 0.96f;
    }
}