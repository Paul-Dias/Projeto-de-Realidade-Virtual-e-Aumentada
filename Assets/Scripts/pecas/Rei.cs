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

        return validMoves;
    }
}