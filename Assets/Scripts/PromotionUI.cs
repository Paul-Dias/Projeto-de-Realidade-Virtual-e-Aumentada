using UnityEngine;
using UnityEngine.UI;

public class PromotionUI : MonoBehaviour
{
    [SerializeField] private Button queenButton;
    [SerializeField] private Button rookButton;
    [SerializeField] private Button bishopButton;
    [SerializeField] private Button knightButton;

    private System.Action<PieceType> onSelected;

    public void Setup(System.Action<PieceType> callback)
    {
        onSelected = callback;
        queenButton.onClick.AddListener(() => Select(PieceType.Rainha));
        rookButton.onClick.AddListener(() => Select(PieceType.Torre));
        bishopButton.onClick.AddListener(() => Select(PieceType.Bispo));
        knightButton.onClick.AddListener(() => Select(PieceType.Cavalo));
        gameObject.SetActive(true);
    }

    private void Select(PieceType type)
    {
        gameObject.SetActive(false);
        onSelected?.Invoke(type);
    }
}
