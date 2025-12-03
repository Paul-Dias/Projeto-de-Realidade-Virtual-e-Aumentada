public class Queen : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
		bool[,] validMoves = new bool[8, 8];

		// Linhas e colunas (torre)
		AddRay(validMoves, 1, 0);
		AddRay(validMoves, -1, 0);
		AddRay(validMoves, 0, 1);
		AddRay(validMoves, 0, -1);

		// Diagonais (bispo)
		AddRay(validMoves, 1, 1);
		AddRay(validMoves, 1, -1);
		AddRay(validMoves, -1, 1);
		AddRay(validMoves, -1, -1);

        return validMoves;
    }

	private void AddRay(bool[,] valid, int dx, int dy)
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
			// Friendly piece blocks
			break;
		}
	}
}