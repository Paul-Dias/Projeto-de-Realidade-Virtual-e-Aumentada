using UnityEngine;

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

        // En Passant
        if (boardManager != null)
        {
            // Verifica se o último movimento foi de um peão inimigo que andou 2 casas
            var lastMoved = boardManager.lastMovedPiece;
            if (lastMoved != null && 
                lastMoved.type == PieceType.Peao && 
                lastMoved.color != color &&
                Mathf.Abs(boardManager.lastMoveToY - boardManager.lastMoveFromY) == 2)
            {
                // Verifica se está ao lado do peão que acabou de se mover
                if (lastMoved.currentY == currentY && Mathf.Abs(lastMoved.currentX - currentX) == 1)
                {
                    // Pode capturar en passant
                    validMoves[lastMoved.currentX, nextY] = true;
                }
            }
        }

        return validMoves;
    }
}