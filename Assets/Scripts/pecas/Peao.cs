public class Pawn : ChessPiece
{
    public override bool[,] GetValidMoves()
    {
        bool[,] validMoves = new bool[8, 8];

        // Direção do movimento (para cima se for branco, para baixo se for preto)
        int direction = (color == PieceColor.Branco) ? 1 : -1;

        // Movimento para frente (1 casa)
        int nextY = currentY + direction;
        if (InBounds(currentX, nextY) && IsEmpty(currentX, nextY))
        {
            validMoves[currentX, nextY] = true;

            // Primeiro movimento: pode andar 2 casas se ambas vazias
            bool onStartRank = (color == PieceColor.Branco && currentY == 1) || (color == PieceColor.Preto && currentY == 6);
            int twoAheadY = currentY + 2 * direction;
            if (onStartRank && InBounds(currentX, twoAheadY) && IsEmpty(currentX, twoAheadY))
            {
                validMoves[currentX, twoAheadY] = true;
            }
        }

        // Lógica de captura na diagonal (exemplo simples)
        // Esquerda
        if (InBounds(currentX - 1, nextY) && IsEnemy(currentX - 1, nextY))
        {
            validMoves[currentX - 1, nextY] = true;
        }
        // Direita
        if (InBounds(currentX + 1, nextY) && IsEnemy(currentX + 1, nextY))
        {
            validMoves[currentX + 1, nextY] = true;
        }

        // Aqui você adicionaria a lógica para o primeiro movimento duplo, en passant, etc.

        return validMoves;
    }
}