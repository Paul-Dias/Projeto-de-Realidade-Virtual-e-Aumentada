using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// IA de Xadrez com dificuldade média.
/// Usa avaliação de material + mobilidade e busca minimax com profundidade limitada.
/// </summary>
public class ChessAI : MonoBehaviour
{
	[Header("AI Settings")]
	[Tooltip("Cor das peças controladas pela IA")]
	[SerializeField] private PieceColor aiColor = PieceColor.Preto;
	
	[Tooltip("Profundidade de busca (2-4 = fácil/médio, 5-6 = difícil)")]
	[SerializeField, Range(1, 6)] private int searchDepth = 3;
	
	[Tooltip("Tempo de espera antes do movimento (segundos)")]
	[SerializeField, Range(0f, 3f)] private float thinkingDelay = 1.0f;
	
	[Header("References")]
	[SerializeField] private BoardManager boardManager;
	
	private bool isThinking = false;
	
	// Valores das peças (centipawns - centésimos de peão)
	private readonly Dictionary<System.Type, int> pieceValues = new Dictionary<System.Type, int>
	{
		{ typeof(Pawn), 100 },
		{ typeof(Cavalo), 320 },
		{ typeof(Bispo), 330 },
		{ typeof(Torre), 500 },
		{ typeof(Queen), 900 },
		{ typeof(Rei), 20000 }
	};
	
	private void Start()
	{
		if (boardManager == null)
		{
			boardManager = FindFirstObjectByType<BoardManager>();
		}
		
		if (boardManager != null)
		{
			boardManager.OnTurnChanged += OnTurnChanged;
			
			// Se a IA joga com brancas, faz o primeiro movimento
			if (aiColor == PieceColor.Branco && boardManager.currentPlayerColor == PieceColor.Branco)
			{
				Invoke(nameof(MakeAIMove), thinkingDelay);
			}
		}
		else
		{
			Debug.LogError("ChessAI: BoardManager não encontrado!");
		}
	}
	
	private void OnDestroy()
	{
		if (boardManager != null)
		{
			boardManager.OnTurnChanged -= OnTurnChanged;
		}
	}
	
	private void OnTurnChanged(PieceColor newPlayer)
	{
		if (newPlayer == aiColor && !isThinking)
		{
			Invoke(nameof(MakeAIMove), thinkingDelay);
		}
	}
	
	private void MakeAIMove()
	{
		if (isThinking) return;
		isThinking = true;
		
		// Busca o melhor movimento
		var bestMove = FindBestMove();
		
		if (bestMove != null)
		{
			// Executa o movimento
			bool success = boardManager.TryMakeMove(bestMove.fromX, bestMove.fromY, bestMove.toX, bestMove.toY);
			
			if (success)
			{
				Debug.Log($"ChessAI: Moveu {bestMove.piece.GetType().Name} de ({bestMove.fromX},{bestMove.fromY}) para ({bestMove.toX},{bestMove.toY}) - Avaliação: {bestMove.score}");
			}
			else
			{
				Debug.LogError($"ChessAI: Falha ao executar movimento!");
			}
		}
		else
		{
			Debug.LogWarning("ChessAI: Nenhum movimento válido encontrado!");
		}
		
		isThinking = false;
	}
	
	private ChessMove FindBestMove()
	{
		List<ChessMove> allMoves = GenerateAllMoves(aiColor);
		
		if (allMoves.Count == 0) return null;
		
		ChessMove bestMove = null;
		float bestScore = float.NegativeInfinity;
		
		// Avalia cada movimento possível
		foreach (var move in allMoves)
		{
			// Simula o movimento
			var capturedPiece = SimulateMove(move);
			
			// Avalia a posição resultante (minimax com profundidade limitada)
			float score = -Minimax(searchDepth - 1, float.NegativeInfinity, float.PositiveInfinity, false);
			
			// Desfaz o movimento
			UndoMove(move, capturedPiece);
			
			// Atualiza o melhor movimento
			if (score > bestScore)
			{
				bestScore = score;
				bestMove = move;
			}
		}
		
		if (bestMove != null)
		{
			bestMove.score = bestScore;
		}
		
		return bestMove;
	}
	
	/// <summary>
	/// Algoritmo Minimax com poda alpha-beta
	/// </summary>
	private float Minimax(int depth, float alpha, float beta, bool isMaximizing)
	{
		if (depth == 0)
		{
			return EvaluatePosition();
		}
		
		PieceColor currentColor = isMaximizing ? aiColor : GetOpponentColor(aiColor);
		List<ChessMove> moves = GenerateAllMoves(currentColor);
		
		if (moves.Count == 0)
		{
			// Sem movimentos = xeque-mate ou empate
			return isMaximizing ? -999999 : 999999;
		}
		
		if (isMaximizing)
		{
			float maxEval = float.NegativeInfinity;
			
			foreach (var move in moves)
			{
				var captured = SimulateMove(move);
				float eval = Minimax(depth - 1, alpha, beta, false);
				UndoMove(move, captured);
				
				maxEval = Mathf.Max(maxEval, eval);
				alpha = Mathf.Max(alpha, eval);
				
				if (beta <= alpha)
					break; // Poda beta
			}
			
			return maxEval;
		}
		else
		{
			float minEval = float.PositiveInfinity;
			
			foreach (var move in moves)
			{
				var captured = SimulateMove(move);
				float eval = Minimax(depth - 1, alpha, beta, true);
				UndoMove(move, captured);
				
				minEval = Mathf.Min(minEval, eval);
				beta = Mathf.Min(beta, eval);
				
				if (beta <= alpha)
					break; // Poda alpha
			}
			
			return minEval;
		}
	}
	
	/// <summary>
	/// Avalia a posição atual do tabuleiro do ponto de vista da IA
	/// Positivo = bom para IA, Negativo = bom para oponente
	/// </summary>
	private float EvaluatePosition()
	{
		float score = 0f;
		
		// 1. Material (valor das peças)
		score += EvaluateMaterial(aiColor);
		score -= EvaluateMaterial(GetOpponentColor(aiColor));
		
		// 2. Mobilidade (número de movimentos válidos)
		score += GenerateAllMoves(aiColor).Count * 10;
		score -= GenerateAllMoves(GetOpponentColor(aiColor)).Count * 10;
		
		// 3. Controle do centro (bônus para peças no centro)
		score += EvaluateCenterControl(aiColor) * 30;
		score -= EvaluateCenterControl(GetOpponentColor(aiColor)) * 30;
		
		return score;
	}
	
	private float EvaluateMaterial(PieceColor color)
	{
		float material = 0f;
		
		foreach (var piece in boardManager.GetPieces(color))
		{
			var pieceType = piece.GetType();
			if (pieceValues.ContainsKey(pieceType))
			{
				material += pieceValues[pieceType];
			}
		}
		
		return material;
	}
	
	private float EvaluateCenterControl(PieceColor color)
	{
		float centerControl = 0f;
		int[] centerSquares = { 27, 28, 35, 36 }; // d4, e4, d5, e5 (em índice linear)
		
		foreach (var piece in boardManager.GetPieces(color))
		{
			int index = piece.currentY * 8 + piece.currentX;
			if (System.Array.IndexOf(centerSquares, index) >= 0)
			{
				centerControl += 1f;
			}
		}
		
		return centerControl;
	}
	
	private List<ChessMove> GenerateAllMoves(PieceColor color)
	{
		List<ChessMove> moves = new List<ChessMove>();
		
		foreach (var piece in boardManager.GetPieces(color))
		{
			bool[,] validMoves = piece.GetValidMoves();
			
			for (int x = 0; x < 8; x++)
			{
				for (int y = 0; y < 8; y++)
				{
					if (validMoves[x, y])
					{
						moves.Add(new ChessMove
						{
							piece = piece,
							fromX = piece.currentX,
							fromY = piece.currentY,
							toX = x,
							toY = y
						});
					}
				}
			}
		}
		
		return moves;
	}
	
	private ChessPiece SimulateMove(ChessMove move)
	{
		// Captura peça se houver
		ChessPiece captured = boardManager.GetPieceAt(move.toX, move.toY);
		
		// Atualiza referências temporariamente
		var oldX = move.piece.currentX;
		var oldY = move.piece.currentY;
		
		move.piece.currentX = move.toX;
		move.piece.currentY = move.toY;
		
		// Não mexe no GameObject 3D durante simulação
		
		return captured;
	}
	
	private void UndoMove(ChessMove move, ChessPiece capturedPiece)
	{
		// Restaura posição da peça
		move.piece.currentX = move.fromX;
		move.piece.currentY = move.fromY;
		
		// Restaura peça capturada (só referência, não recria GameObject)
	}
	
	private PieceColor GetOpponentColor(PieceColor color)
	{
		return color == PieceColor.Branco ? PieceColor.Preto : PieceColor.Branco;
	}
}

/// <summary>
/// Estrutura para representar um movimento de xadrez
/// </summary>
public class ChessMove
{
	public ChessPiece piece;
	public int fromX;
	public int fromY;
	public int toX;
	public int toY;
	public float score;
}
