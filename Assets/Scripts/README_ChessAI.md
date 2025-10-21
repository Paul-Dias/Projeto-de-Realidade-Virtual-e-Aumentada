# ChessAI - IA de Xadrez com Dificuldade Média

Sistema de inteligência artificial para jogar xadrez no Unity, com nível de dificuldade configurável.

## ✅ Correções Aplicadas

Todos os erros e warnings foram corrigidos:

1. ✅ **AR Foundation 6.0**: Atualizado `trackedImagesChanged` → `trackablesChanged`
2. ✅ **ARTrackedImagesChangedEventArgs obsoleto**: Substituído por `ARTrackablesChangedEventArgs<ARTrackedImage>`
3. ✅ **Método `TryMakeMove` faltando**: Implementado com assinatura correta `(fromX, fromY, toX, toY)`
4. ✅ **XROrigin não encontrado**: Substituído por busca genérica de câmera em hierarquia XR
5. ✅ **Campo não usado**: Removido `enableDebugLogs` de `JumpOnTouch.cs`

## 🎮 Como Usar a IA

### Passo 1: Adicionar o Script ChessAI

1. Na cena, crie um GameObject vazio (ou use o BoardManager):
   - GameObject > Create Empty
   - Nome: "ChessAI"
2. Adicione o componente `ChessAI`:
   - Add Component > ChessAI

### Passo 2: Configurar no Inspector

No Inspector do `ChessAI`:

- **AI Color**: `Preto` (ou `Branco` se quiser que a IA jogue com brancas)
- **Search Depth**: 
  - `2` = Fácil (rápido, decisões simples)
  - `3` = **Médio** (padrão recomendado)
  - `4` = Difícil (lento, joga melhor)
  - `5-6` = Muito difícil (muito lento)
- **Thinking Delay**: `1.0` segundos (tempo antes de mover)
- **Board Manager**: Arraste o GameObject com `BoardManager`

### Passo 3: Testar

1. Play
2. Você joga com brancas (ou pretas, dependendo da configuração)
3. A IA responde automaticamente após seu movimento
4. Console mostra os movimentos da IA com avaliação

## 🧠 Como a IA Funciona

### Algoritmo: Minimax com Poda Alpha-Beta

A IA usa o algoritmo clássico de xadrez **Minimax** com otimização:

1. **Geração de Movimentos**: Usa `piece.GetValidMoves()` para cada peça
2. **Simulação**: Testa cada movimento sem mover os GameObjects
3. **Avaliação**: Calcula pontuação da posição resultante
4. **Busca em Profundidade**: Olha N jogadas à frente (definido por `searchDepth`)
5. **Poda Alpha-Beta**: Descarta ramos ruins para acelerar

### Avaliação de Posição

A IA considera três fatores:

#### 1. Material (Valor das Peças)
- Peão = 100
- Cavalo = 320
- Bispo = 330
- Torre = 500
- Rainha = 900
- Rei = 20.000

#### 2. Mobilidade
- +10 pontos por cada movimento válido disponível
- Mais opções = melhor posição

#### 3. Controle do Centro
- +30 pontos por peça nas casas centrais (d4, e4, d5, e5)
- Princípio clássico de xadrez

### Profundidade de Busca

| Depth | Dificuldade | Jogadas à Frente | Performance |
|-------|-------------|------------------|-------------|
| 1     | Muito Fácil | 1 jogada         | Instantâneo |
| 2     | Fácil       | 2 jogadas        | Rápido      |
| **3** | **Médio**   | **3 jogadas**    | **Bom**     |
| 4     | Difícil     | 4 jogadas        | Lento       |
| 5-6   | Expert      | 5-6 jogadas      | Muito lento |

**Recomendação**: Use `searchDepth = 3` para dificuldade média equilibrada.

## 🔧 Personalização

### Ajustar Dificuldade Dinamicamente

```csharp
// Obter referência ao ChessAI
ChessAI ai = FindFirstObjectByType<ChessAI>();

// Mudar dificuldade em runtime
ai.searchDepth = 4; // Deixa mais difícil

// Mudar velocidade de resposta
ai.thinkingDelay = 0.5f; // Mais rápido
```

### Adicionar Mais Fatores de Avaliação

Edite o método `EvaluatePosition()` em `ChessAI.cs`:

```csharp
private float EvaluatePosition()
{
    float score = 0f;
    
    // Material
    score += EvaluateMaterial(aiColor);
    score -= EvaluateMaterial(GetOpponentColor(aiColor));
    
    // Mobilidade
    score += GenerateAllMoves(aiColor).Count * 10;
    score -= GenerateAllMoves(GetOpponentColor(aiColor)).Count * 10;
    
    // Centro
    score += EvaluateCenterControl(aiColor) * 30;
    score -= EvaluateCenterControl(GetOpponentColor(aiColor)) * 30;
    
    // ADICIONE AQUI:
    // - Estrutura de peões
    // - Segurança do rei
    // - Par de bispos
    // - Torres em colunas abertas
    // etc.
    
    return score;
}
```

### Mudar Valores das Peças

Edite o dicionário `pieceValues` em `ChessAI.cs`:

```csharp
private readonly Dictionary<System.Type, int> pieceValues = new Dictionary<System.Type, int>
{
    { typeof(Pawn), 100 },
    { typeof(Cavalo), 300 },  // Ajuste aqui
    { typeof(Bispo), 350 },   // Bispo > Cavalo
    { typeof(Torre), 500 },
    { typeof(Queen), 950 },   // Rainha mais valiosa
    { typeof(Rei), 20000 }
};
```

## 🎯 Limitações Atuais

A IA **não considera ainda**:
- Xeque e xeque-mate (joga normalmente)
- Roque
- En passant
- Promoção de peão
- Empate por repetição ou 50 movimentos

Isso significa que a IA pode fazer movimentos que deixam o próprio rei em xeque. Para uma IA completa, seria necessário validar legalidade dos movimentos (não deixar rei em xeque).

## 📊 Performance

Complexidade por profundidade (aproximado):

- Depth 1: ~20-40 posições avaliadas
- Depth 2: ~400-1,600 posições
- Depth 3: ~8,000-64,000 posições
- Depth 4: ~160,000-2,500,000 posições

A poda alpha-beta reduz isso significativamente (50-90% menos avaliações).

## 🚀 Próximos Passos

Para melhorar a IA:

1. **Validação de xeque**: Rejeitar movimentos que deixam o rei em xeque
2. **Ordenação de movimentos**: Avaliar capturas primeiro (melhora poda)
3. **Tabela de transposição**: Cache de posições já avaliadas
4. **Quiescence search**: Avaliar capturas mais profundamente
5. **Opening book**: Base de dados de aberturas
6. **Endgame tablebases**: Soluções perfeitas para finais

## 🐛 Troubleshooting

### "IA não move"
- ✅ Certifique-se que `AI Color` corresponde ao turno
- ✅ Verifique se `Board Manager` está atribuído
- ✅ Console mostra erros?

### "IA move errado/ilegal"
- Isso é esperado sem validação de xeque
- Implemente validação antes de aceitar movimentos

### "Muito lento"
- Reduza `Search Depth` para 2 ou 3
- Performance depende do hardware

### "IA sempre perde"
- Aumente `Search Depth` para 4+
- Ajuste pesos de avaliação

---

Qualquer dúvida, só perguntar! 🎯
