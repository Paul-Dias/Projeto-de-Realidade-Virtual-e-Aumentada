public class Bispo : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
        bool[,] validMoves = new bool[8, 8];

		int x = currentX;
		int y = currentY;

		// Quatro diagonais, parando no primeiro bloqueio
		AddDiagonalRay(validMoves, 1, 1);   // NE
		AddDiagonalRay(validMoves, 1, -1);  // SE
		AddDiagonalRay(validMoves, -1, 1);  // NO
		AddDiagonalRay(validMoves, -1, -1); // SO

		return validMoves;
    }

	private void AddDiagonalRay(bool[,] valid, int dx, int dy)
	{
		for (int step = 1; step < 8; step++)
		{
			int nx = currentX + dx * step;
			int ny = currentY + dy * step;
			if (!InBounds(nx, ny)) break;

			if (IsEmpty(nx, ny))
			{
				valid[nx, ny] = true;
				continue;
			}
			if (IsEnemy(nx, ny))
			{
				valid[nx, ny] = true;
				break;
			}
			// Friendly piece blocks further movement
			break;
		}
	}
}