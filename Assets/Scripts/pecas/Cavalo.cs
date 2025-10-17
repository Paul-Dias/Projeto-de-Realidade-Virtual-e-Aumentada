public class Cavalo : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
		bool[,] validMoves = new bool[8, 8];

		int x = currentX;
		int y = currentY;

		// Movimentos em "L"
		if (hasEnemyOrBlank(x + 2, y + 1)) validMoves[x + 2, y + 1] = true; // Direita cima
		if (hasEnemyOrBlank(x + 2, y - 1)) validMoves[x + 2, y - 1] = true; // Direita baixo

		if (hasEnemyOrBlank(x - 2, y + 1)) validMoves[x - 2, y + 1] = true; // Esquerda cima
		if (hasEnemyOrBlank(x - 2, y - 1)) validMoves[x - 2, y - 1] = true; // Esquerda baixo

		if (hasEnemyOrBlank(x + 1, y + 2)) validMoves[x + 1, y + 2] = true; // Cima direita
		if (hasEnemyOrBlank(x + 1, y - 2)) validMoves[x + 1, y - 2] = true; // Baixo direita

		if (hasEnemyOrBlank(x - 1, y + 2)) validMoves[x - 1, y + 2] = true; // Cima esquerda
		if (hasEnemyOrBlank(x - 1, y - 2)) validMoves[x - 1, y - 2] = true; // Baixo esquerda

		return validMoves;
    }
}