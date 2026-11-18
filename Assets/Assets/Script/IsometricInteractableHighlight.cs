using UnityEngine;

public class IsometricInteractableHighlight : MonoBehaviour
{
    private Material _mat;
    private Color _defaultOutline;

    [Header("Highlight Settings")]
    public Color hoverRimColor = new Color(1f, 0.95f, 0.6f, 0.85f);
    public Color hoverOutlineColor = Color.white;

    void Start()
    {
        // Menggunakan .material agar hanya mengubah instance objek ini saja
        _mat = GetComponent<Renderer>().material;
        _defaultOutline = _mat.GetColor("_OutlineColor");
    }

    void OnMouseEnter()
    {
        _mat.SetColor("_RimColor", hoverRimColor);
        _mat.SetColor("_OutlineColor", hoverOutlineColor);
    }

    void OnMouseExit()
    {
        // Kembalikan Alpha Rim ke 0 untuk mematikan rim light
        _mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0f));
        _mat.SetColor("_OutlineColor", _defaultOutline);
    }
}