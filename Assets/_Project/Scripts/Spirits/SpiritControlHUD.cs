using System;
using UnityEngine;
using UnityEngine.UI;

public class SpiritControlHUD : MonoBehaviour
{
    [Serializable]
    public class KeyButtonSlot
    {
        public string label;
        [Tooltip("Objek visual saat tombol tidak ditekan (Default).")]
        public GameObject defaultStateObject;
        [Tooltip("Objek visual saat tombol sedang ditekan (Pressed).")]
        public GameObject pressedStateObject;

        [Header("Opsional: Jika memakai 1 Image dengan swap Sprite")]
        public Image targetImage;
        public Sprite defaultSprite;
        public Sprite pressedSprite;

        public void SetPressed(bool isPressed)
        {
            if (defaultStateObject != null)
                defaultStateObject.SetActive(!isPressed);

            if (pressedStateObject != null)
                pressedStateObject.SetActive(isPressed);

            if (targetImage != null)
            {
                Sprite chosen = isPressed ? pressedSprite : defaultSprite;
                if (chosen != null) targetImage.sprite = chosen;
            }
        }
    }

    [SerializeField] private SpiritKeySelector _keySelector;
    [Tooltip("Urutan: [0] Tombol 1, [1] Tombol 2, [2] Tombol 3, [3] Tombol Space")]
    [SerializeField] private KeyButtonSlot[] _slots = new KeyButtonSlot[4];

    private void Awake()
    {
        if (_keySelector == null)
            _keySelector = FindFirstObjectByType<SpiritKeySelector>();
    }

    private void OnEnable()
    {
        if (_keySelector != null)
            _keySelector.OnKeyStateChanged += HandleKeyStateChanged;
    }

    private void OnDisable()
    {
        if (_keySelector != null)
            _keySelector.OnKeyStateChanged -= HandleKeyStateChanged;
    }

    private void Start()
    {
        // Tampilkan seluruh UI dalam kondisi Default sejak game dimulai
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] != null)
                _slots[i].SetPressed(false);
        }
    }

    private void HandleKeyStateChanged(int slotIndex, bool isPressed)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;
        if (_slots[slotIndex] != null)
            _slots[slotIndex].SetPressed(isPressed);
    }
}