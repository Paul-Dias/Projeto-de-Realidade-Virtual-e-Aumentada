using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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

	private readonly Dictionary<System.Type, int> pieceValues = new()
	{
		{ typeof(Pawn), 100 },
		{ typeof(Cavalo), 320 },
		{ typeof(Bispo), 330 },
		{ typeof(Torre), 500 },
		{ typeof(Queen), 900 },
		{ typeof(Rei), 20000 }
	};

	// Evitar movimentos de vai-e-volta e melhorar segurança do rei
	private ChessMove lastMove;
	[SerializeField, Range(0f, 1000f)] private float repetitionPenalty = 200f;
	[SerializeField, Range(0f, 2000f)] private float kingInCheckPenalty = 1500f;
	[SerializeField, Range(0f, 500f)] private float kingZoneThreatPenalty = 50f;

	private void Start()
	{
		if (!boardManager) boardManager = FindFirstObjectByType<BoardManager>();
		if (!boardManager) { Debug.LogError("ChessAI: BoardManager não encontrado!"); return; }

		boardManager.OnTurnChanged += OnTurnChanged;
		if (aiColor == boardManager.currentPlayerColor)
			Invoke(nameof(MakeAIMove), thinkingDelay);
	}

	private void OnDestroy()
	{
		if (boardManager) boardManager.OnTurnChanged -= OnTurnChanged;
	}

	private void OnTurnChanged(PieceColor newPlayer)
	{
		if (newPlayer == aiColor && !isThinking)
			Invoke(nameof(MakeAIMove), thinkingDelay);
	}

	private void MakeAIMove()
	{
		if (isThinking) return;
		isThinking = true;

		var bestMove = FindBestMove();

		if (bestMove != null)
		{
			bool success = boardManager.TryMakeMove(bestMove.fromX, bestMove.fromY, bestMove.toX, bestMove.toY);
			Debug.Log(success
				? $"AI: {bestMove.piece.GetType().Name} ({bestMove.fromX},{bestMove.fromY})→({bestMove.toX},{bestMove.toY}) [{bestMove.score:F0}]"
				: "AI: Falha ao mover!");

			if (success)
			{
				lastMove = bestMove; // Atualiza último movimento para evitar vai-e-volta
			}
		}
		else Debug.LogWarning("AI: Nenhum movimento válido.");

		isThinking = false;
	}

	private ChessMove FindBestMove()
	{
		var allMoves = GenerateAllMoves(aiColor);
		if (allMoves.Count == 0) return null;

		float bestScore = float.NegativeInfinity;
		ChessMove bestMove = null;

		foreach (var move in allMoves)
		{
			var captured = SimulateMove(move);
			float score = -Minimax(searchDepth - 1, float.NegativeInfinity, float.PositiveInfinity, false);

			// Penaliza repetição imediata (voltar para a casa anterior)
			if (lastMove != null &&
				move.piece == lastMove.piece &&
				move.fromX == lastMove.toX && move.fromY == lastMove.toY &&
				move.toX == lastMove.fromX && move.toY == lastMove.fromY)
			{
				score -= repetitionPenalty;
			}
			UndoMove(move, captured);

			if (score > bestScore)
			{
				bestScore = score;
				bestMove = move;
			}
		}

		if (bestMove != null) bestMove.score = bestScore;
		return bestMove;
	}

	private float Minimax(int depth, float alpha, float beta, bool isMaximizing)
	{
		if (depth == 0) return EvaluatePosition();

		PieceColor current = isMaximizing ? aiColor : GetOpponentColor(aiColor);
		var moves = GenerateAllMoves(current);

		if (moves.Count == 0)
			return isMaximizing ? -999999 : 999999;

		if (isMaximizing)
		{
			float maxEval = float.NegativeInfinity;
			foreach (var m in moves)
			{
				var captured = SimulateMove(m);
				float eval = Minimax(depth - 1, alpha, beta, false);
				UndoMove(m, captured);

				maxEval = Mathf.Max(maxEval, eval);
				alpha = Mathf.Max(alpha, eval);
				if (beta <= alpha) break;
			}
			return maxEval;
		}
		else
		{
			float minEval = float.PositiveInfinity;
			foreach (var m in moves)
			{
				var captured = SimulateMove(m);
				float eval = Minimax(depth - 1, alpha, beta, true);
				UndoMove(m, captured);

				minEval = Mathf.Min(minEval, eval);
				beta = Mathf.Min(beta, eval);
				if (beta <= alpha) break;
			}
			return minEval;
		}
	}

	private float EvaluatePosition()
	{
		float score = 0f;

		var myPieces = boardManager.GetPieces(aiColor);
		var enemyPieces = boardManager.GetPieces(GetOpponentColor(aiColor));

		// Material
		score += myPieces.Sum(p => pieceValues[p.GetType()]);
		score -= enemyPieces.Sum(p => pieceValues[p.GetType()]);

		// Mobilidade
		int myMoves = myPieces.Sum(p => p.GetValidMoves().Cast<bool>().Count(v => v));
		int oppMoves = enemyPieces.Sum(p => p.GetValidMoves().Cast<bool>().Count(v => v));
		score += (myMoves - oppMoves) * 5;

		// Centro
		score += (EvaluateCenterControl(aiColor) - EvaluateCenterControl(GetOpponentColor(aiColor))) * 25;

		// Desenvolvimento
		score += EvaluateDevelopment(myPieces) * 3;

		// Segurança do Rei (priorizar defesa)
		score += EvaluateKingSafety(aiColor);
		score -= EvaluateKingSafety(GetOpponentColor(aiColor));

		// Ameaças (capturas possíveis)
		score += EvaluateThreats(aiColor) * 10;
		score -= EvaluateThreats(GetOpponentColor(aiColor)) * 10;

		return score;
	}

	private float EvaluateKingSafety(PieceColor color)
	{
		var king = GetKing(color);
		if (king == null) return 0f;

		int kingX = king.currentX;
		int kingY = king.currentY;

		// Penalidade se o rei estiver sob ataque direto (estimativa via movimentos inimigos atuais)
		PieceColor opponent = GetOpponentColor(color);
		float penalty = 0f;

		foreach (var p in boardManager.GetPieces(opponent))
		{
			var moves = p.GetValidMoves();
			if (moves[kingX, kingY])
			{
				penalty += kingInCheckPenalty;
			}

			// Penaliza ataques à zona ao redor do rei (8 casas adjacentes)
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					int nx = kingX + dx;
					int ny = kingY + dy;
					if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
					{
						if (moves[nx, ny]) penalty += kingZoneThreatPenalty;
					}
				}
			}
		}

		// Pequeno bônus por peças amigas protegendo a zona do rei (bloqueando ou ocupando casas adjacentes)
		float protection = 0f;
		foreach (var p in boardManager.GetPieces(color))
		{
			var moves = p.GetValidMoves();
			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0) continue;
					int nx = kingX + dx;
					int ny = kingY + dy;
					if (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
					{
						if (moves[nx, ny]) protection += 10f; // pequeno bônus
					}
				}
			}
		}

		return -penalty + protection;
	}

	private ChessPiece GetKing(PieceColor color)
	{
		foreach (var p in boardManager.GetPieces(color))
		{
			if (p.GetType() == typeof(Rei)) return p;
		}
		return null;
	}

	private float EvaluateCenterControl(PieceColor color)
	{
		int[] center = { 27, 28, 35, 36 };
		return boardManager.GetPieces(color)
			.Count(p => center.Contains(p.currentY * 8 + p.currentX));
	}

	private float EvaluateDevelopment(IEnumerable<ChessPiece> pieces)
	{
		// Penaliza peças ainda na linha inicial (menos mobilidade)
		return pieces.Count(p => p.currentY != (p.color == PieceColor.Branco ? 1 : 6));
	}

	private int EvaluateThreats(PieceColor color)
	{
		int threats = 0;
		foreach (var p in boardManager.GetPieces(color))
		{
			var moves = p.GetValidMoves();
			for (int x = 0; x < 8; x++)
				for (int y = 0; y < 8; y++)
					if (moves[x, y] && boardManager.GetPieceAt(x, y) != null)
						threats++;
		}
		return threats;
	}

	private List<ChessMove> GenerateAllMoves(PieceColor color)
	{
		var moves = new List<ChessMove>();
		foreach (var p in boardManager.GetPieces(color))
		{
			var valid = p.GetValidMoves();
			for (int x = 0; x < 8; x++)
				for (int y = 0; y < 8; y++)
					if (valid[x, y])
						moves.Add(new ChessMove { piece = p, fromX = p.currentX, fromY = p.currentY, toX = x, toY = y });
		}
		return moves;
	}

	private ChessPiece SimulateMove(ChessMove move)
	{
		var captured = boardManager.GetPieceAt(move.toX, move.toY);
		move.piece.currentX = move.toX;
		move.piece.currentY = move.toY;
		return captured;
	}

	private void UndoMove(ChessMove move, ChessPiece captured)
	{
		move.piece.currentX = move.fromX;
		move.piece.currentY = move.fromY;
	}

	private PieceColor GetOpponentColor(PieceColor color)
	{
		return color == PieceColor.Branco ? PieceColor.Preto : PieceColor.Branco;
	}
}

public class ChessMove
{
	public ChessPiece piece;
	public int fromX, fromY, toX, toY;
	public float score;
}
