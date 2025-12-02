public class Rei : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
		bool[,] validMoves = new bool[8, 8];

		int x = currentX;
		int y = currentY;

		if (x + 1 < 8)
		{
			if (hasEnemyOrBlank(x + 1, y))
			{
				validMoves[x + 1, y] = true;
			}
			if (y + 1 < 8)
			{
				if (hasEnemyOrBlank(x + 1, y + 1))
				{
					validMoves[x + 1, y + 1] = true; // Diagonal superior direita
				}
			}
			if (y - 1 >= 0)
			{
				if (hasEnemyOrBlank(x + 1, y - 1))
				{
					validMoves[x + 1, y - 1] = true; // Diagonal inferior direita
				}
			}
		}
		if (x - 1 >= 0)
		{
			if (hasEnemyOrBlank(x - 1, y))
			{
				validMoves[x - 1, y] = true;
			}
			if (y + 1 < 8)
			{
				if (hasEnemyOrBlank(x - 1, y + 1))
				{
					validMoves[x - 1, y + 1] = true; // Diagonal superior esquerda
				}
			}
			if (y - 1 >= 0)
			{
				if (hasEnemyOrBlank(x - 1, y - 1))
				{
					validMoves[x - 1, y - 1] = true; // Diagonal inferior esquerda
				}
			}
		}

		if (y + 1 < 8)
		{
			if (hasEnemyOrBlank(x, y + 1))
			{
				validMoves[x, y + 1] = true; // Cima
			}
		}
		if (y - 1 >= 0)
		{
			if (hasEnemyOrBlank(x, y - 1))
			{
				validMoves[x, y - 1] = true; // Baixo
			}
		}

		// Roque (Castling)
		if (moveCount == 0 && boardManager != null)
		{
			int kingY = currentY;
			
			// Roque do lado do Rei (direita)
			ChessPiece rookRight = boardManager.GetPieceAt(7, kingY);
			if (rookRight != null && rookRight.type == PieceType.Torre && 
			    rookRight.color == color && rookRight.moveCount == 0)
			{
				// Verifica se o caminho está livre
				if (IsEmpty(5, kingY) && IsEmpty(6, kingY))
				{
					validMoves[6, kingY] = true; // Rei move 2 casas para direita
				}
			}
			
			// Roque do lado da Rainha (esquerda)
			ChessPiece rookLeft = boardManager.GetPieceAt(0, kingY);
			if (rookLeft != null && rookLeft.type == PieceType.Torre && 
			    rookLeft.color == color && rookLeft.moveCount == 0)
			{
				// Verifica se o caminho está livre
				if (IsEmpty(1, kingY) && IsEmpty(2, kingY) && IsEmpty(3, kingY))
				{
					validMoves[2, kingY] = true; // Rei move 2 casas para esquerda
				}
			}
		}

        return validMoves;
    }
}