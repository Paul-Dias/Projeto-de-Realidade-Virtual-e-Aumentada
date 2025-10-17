public class Torre : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
		bool[,] validMoves = new bool[8, 8];

		// Quatro direções ortogonais com parada correta
		AddLineRay(validMoves, 1, 0);   // +x
		AddLineRay(validMoves, -1, 0);  // -x
		AddLineRay(validMoves, 0, 1);   // +y
		AddLineRay(validMoves, 0, -1);  // -y

        return validMoves;
    }

	private void AddLineRay(bool[,] valid, int dx, int dy)
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
			// Peça amiga bloqueia
			break;
		}
	}
}