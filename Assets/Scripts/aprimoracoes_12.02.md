# Aprimoracoes 12/02

Tempo total: IA Rodou em 02 minutos 20 segundos para realizar isso, utilizando o modelo GPT-5.

## 0. Contador de movimentos
- Adicionado `moveCount` em `ChessPiece` e incremento ao final de cada movimento em `BoardManager.AnimateMove`.
- Linhas alteradas (aprox.):
  - `ChessPieces.cs`: + entre 18-28 (propriedade `moveCount`).
  - `BoardManager.cs`: + entre 290-310 (incremento `moveCount++`).

## 1. En Passant
- Adicionada detecção de último movimento do oponente (`lastMovedPiece`, `lastMoveFromY`, `lastMoveToY`).
- `Peao.cs` atualizado para marcar movimentos válidos de en passant.
- `BoardManager.AnimateMove` realiza a captura en passant quando aplicável.
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 40-60 (novos campos), + entre 330-390 (lógica en passant), + entre 360-380 (rastreamento do último movimento).
  - `Peao.cs`: + entre 40-80 (marcar en passant como válido).

## 2. Roque (Castling)
- `Rei.cs` passou a marcar movimentos de roque quando rei e torre têm `moveCount == 0` e caminho livre.
- `BoardManager.AnimateMove` move a torre automaticamente quando o rei se move duas casas.
- Linhas alteradas (aprox.):
  - `Rei.cs`: + entre 60-110 (marcar casas de roque).
  - `BoardManager.cs`: + entre 390-450 (mover torre no roque).

## 3. Cheque e filtro de movimentos
- Adicionados métodos `IsKingInCheck`, `WouldLeaveKingInCheck` e `FilterMovesForCheck` no `BoardManager`.
- `SelectPiece`, `CanMakeMove` e `TryMakeMove` agora filtram movimentos que deixariam o próprio rei em cheque.
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 500-640 (novos métodos), + ajustes em `SelectPiece`/`CanMakeMove`/`TryMakeMove`.

## 4. Cheque-Mate + UI
- Adicionado campo `checkMateUI` como `[SerializeField]` e inicialização para ficar desativado no `Start`.
- Após `ToggleTurn`, verifica `IsCheckMate` e exibe a UI em `OnCheckMate`.
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 120-140 (serializeField UI), + entre 200-210 (desativar no Start), + entre 650-710 (`IsCheckMate`, `OnCheckMate`), + em `ToggleTurn` para checar fim de jogo.

## 5. Ajustes de animação (Cavalo e Roque)
- Altura do pulo do cavalo aumentada (`horseJumpHeight = 0.5f`) para arco visível.
- Roque agora move a torre com animação suave em paralelo ao rei (lerp sincronizado com `moveDuration`).
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 95-100 (altura do cavalo), + entre 360-420 (preparação da animação da torre), + entre 430-470 (animação paralela), + entre 500-530 (finalização estado da torre).

## 6. Promoção de Peão
- Implementada promoção com UI (seleção: Rainha, Torre, Bispo, Cavalo).
- `BoardManager`: fluxo `BeginPromotion` → `OnPromotionSelected` → `PromotePawnTo` substitui o peão pelo tipo escolhido no mesmo GameObject e atualiza `boardState`.
- `PromotionUI.cs`: script novo que aciona as opções de promoção via botões.
- Turno só troca após a animação e a promoção concluída.
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 520-570 (chamada BeginPromotion), + entre 570-640 (métodos de promoção), + `ToggleTurnAfterAnimation` passa a aguardar `isPromotionPending`.
  - `PromotionUI.cs`: arquivo novo.

## 7. UI de Cheque-Mate (vencedor)
- `BoardManager.OnCheckMate` agora escreve o texto do vencedor em `checkMateLabel` (UI.Text) ou procura dinamicamente em `checkMateUI`.
- Linhas alteradas (aprox.):
  - `BoardManager.cs`: + entre 70-90 (campo `checkMateLabel`), + entre 900-930 (atualização do texto antes de exibir a UI).

