using UnityEngine;

public abstract class ChessPiece : MonoBehaviour
{
	[Header("Piece Info")]
	[SerializeField] public PieceColor color;
	[Tooltip("Tipo da peça de xadrez")]
	[SerializeField] public PieceType type;

    // Posição da peça na matriz do tabuleiro (ex: 0 a 7)
    public int currentX { get; set; }
    public int currentY { get; set; }

	// Contador de movimentos da peça (usado para roque, en passant, etc.)
	public int moveCount { get; set; } = 0;

	// Referência ao gerenciador do jogo
	protected BoardManager boardManager;

	private void SetupCollision()
	{
		// // Pegando todos os "filhos" da peça (partes do modelo 3D) e adicionando colisores "MeshCollider" se não existirem
		// MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
		// foreach (var renderer in renderers)
		// {
		// 	if (renderer.gameObject.GetComponent<Collider>() == null)
		// 	{
		// 		var collider = renderer.gameObject.AddComponent<MeshCollider>();
		// 		collider.convex = true; // Necessário para colisores em objetos móveis
		// 		collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
		// 	}
		// }
	}

	public bool hasEnemyOrBlank(int x, int y)
	{
		if (x < 0 || x >= 8 || y < 0 || y >= 8) return false; // Fora dos limites do tabuleiro
		ChessPiece targetPiece = boardManager.GetPieceAt(x, y);
		return targetPiece == null || targetPiece.color != this.color;
	}

	// Helpers para facilitar leitura das regras de movimento
	protected bool InBounds(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;
	protected ChessPiece GetAt(int x, int y) => InBounds(x, y) ? boardManager.GetPieceAt(x, y) : null;
	protected bool IsEmpty(int x, int y) => GetAt(x, y) == null;
	protected bool IsEnemy(int x, int y)
	{
		var p = GetAt(x, y);
		return p != null && p.color != this.color;
	}
	protected bool IsFriendly(int x, int y)
	{
		var p = GetAt(x, y);
		return p != null && p.color == this.color;
	}

	/// <summary>
	/// Inicializa a peça com sua posição e referência ao board.
	/// </summary>
	public void Setup(int x, int y, BoardManager manager)
	{
		currentX = x;
		currentY = y;
		boardManager = manager;

		SetupCollision();
	}

    /// <summary>
    /// Método abstrato que DEVE ser implementado por cada tipo de peça.
    /// Ele retorna uma matriz booleana representando os movimentos válidos.
    /// </summary>
    public abstract bool[,] GetValidMoves();

	/// <summary>
	/// Efeito visual de pulo. Pode ser chamado pelo BoardManager quando a peça for selecionada.
	/// </summary>
	public void JumpEffect()
	{
		Debug.Log($"JumpEffect: {gameObject.name} pulou!");
	}

	public void OnTouch()
	{
		JumpEffect();
	}
}