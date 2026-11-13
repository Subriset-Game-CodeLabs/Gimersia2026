using UnityEngine;

public class SpiritView : MonoBehaviour
{
    [SerializeField] private GameObject _selectionRing;

    public int Index { get; private set; }

    public void Initialize(int index)
    {
        Index = index;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        _selectionRing.SetActive(selected);
    }
}
