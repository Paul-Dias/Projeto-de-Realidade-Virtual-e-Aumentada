# BoardManager - Configuração para AR e VR

O `BoardManager` agora suporta **três modos de jogo**:
1. **Standalone Mobile** - Celular com rotação por tilt (giroscópio)
2. **AR Image Tracking** - AR móvel com detecção de QR/marcador
3. **VR Headset** - Óculos VR com capacidade de pegar e rotacionar o tabuleiro

---

## 📱 Modo 1: Standalone Mobile

**O que faz:**
- Usa o acelerômetro do celular para inclinar o tabuleiro
- Touch para selecionar e mover peças

**Setup no Unity:**
1. No Inspector do `BoardManager`:
   - **Play Mode** = `StandaloneMobile`
   - **Enable Device Rotation** = ✅
   - Ajuste `Max Tilt Angle`, `Rotation Sensitivity` e `Rotation Smoothing`
2. Garanta que a câmera principal está marcada como `MainCamera`
3. Build Settings > Android/iOS

**Testar no Editor:**
- Use mouse para clicar nas peças
- Tilt não funciona no editor, só no dispositivo

---

## 🎯 Modo 2: AR Image Tracking (QR/Marcador)

**O que faz:**
- Detecta uma imagem de referência (QR code ou marcador impresso)
- Projeta o tabuleiro sobre o marcador detectado
- Segue o marcador se ele se mover

**Setup no Unity:**

### Passo 1: Criar Reference Image Library
1. `Assets > Create > XR > Reference Image Library`
2. Clique no asset criado
3. Clique em "Add Image"
4. Arraste sua imagem de QR/marcador
5. Defina o **nome** como `ChessMarker` (ou o nome que você configurar no Inspector)
6. Defina as dimensões físicas reais (ex: 0.2m x 0.2m)

### Passo 2: Configurar AR Session
1. Na cena, crie/tenha:
   - **AR Session** (GameObject > XR > AR Session)
   - **AR Session Origin** ou **XR Origin** (com AR Camera)
   - **AR Tracked Image Manager** (adicione ao AR Session Origin):
     - Em "Serialized Library", arraste sua Reference Image Library
     - Em "Max Number Of Moving Images" = 1 ou mais

### Passo 3: Configurar BoardManager
1. No Inspector do `BoardManager`:
   - **Play Mode** = `AR_ImageTracking`
   - **Target Image Name** = `ChessMarker` (mesmo nome da imagem na library)
   - **Tracked Image Manager** = arraste o componente ARTrackedImageManager
2. Garanta que o `BoardManager` está no root ou em um GameObject que pode ser movido

### Passo 4: Build e Testar
1. Build Settings > Android/iOS
2. Player Settings > XR Plug-in Management > ✅ ARCore (Android) ou ARKit (iOS)
3. Imprima o QR/marcador usado na Reference Image Library
4. Aponte a câmera para o marcador impresso
5. O tabuleiro aparecerá sobre o marcador

**Dicas:**
- Marcadores com alto contraste e bordas definidas funcionam melhor
- Imprima em tamanho grande (15cm+) para melhor detecção
- Ilumine bem o ambiente

---

## 🥽 Modo 3: VR Headset (Meta Quest, HTC Vive, etc.)

**O que faz:**
- Permite pegar o tabuleiro com os controles VR
- Girar, examinar e inspecionar de todos os ângulos
- Seleção de peças com raio/ponteiro VR

**Setup no Unity:**

### Passo 1: Configurar XR Plugin
1. Window > Package Manager > instalar:
   - **XR Interaction Toolkit**
   - **XR Plugin Management**
   - Plugin do seu headset (Oculus XR, OpenXR, etc.)
2. Edit > Project Settings > XR Plug-in Management:
   - ✅ Marque o plugin do seu headset (ex: Oculus)

### Passo 2: Adicionar XR Origin
1. GameObject > XR > XR Origin (Action-based) ou Device-based
2. Isso cria:
   - XR Origin
   - Main Camera (filha)
   - LeftHand Controller e RightHand Controller

### Passo 3: Configurar Interação
1. Garanta que os controllers têm:
   - **XR Ray Interactor** ou **XR Direct Interactor**
   - **XR Interactor Line Visual** (para mostrar o raio)

### Passo 4: Configurar BoardManager
1. No Inspector do `BoardManager`:
   - **Play Mode** = `VR_Headset`
   - **Enable VR Grab** = ✅
2. O script automaticamente adiciona `XRGrabInteractable` ao tabuleiro
3. Adicione um **Rigidbody** ao GameObject do BoardManager:
   - Use Gravity = ❌
   - Is Kinematic = ✅ (ou deixe dinâmico se quiser física)

### Passo 5: Colisores
1. Adicione um **Box Collider** ao GameObject do BoardManager que envolva todo o tabuleiro
   - Isso permite que o VR controller "pegue" o tabuleiro

### Passo 6: Testar
1. Build para seu headset (Android para Quest, Windows para PCVR)
2. Use o controller para apontar para o tabuleiro
3. Aperte o gatilho/grip para pegar
4. Mova e gire livremente
5. Solte para fixar

**Seleção de Peças no VR:**
- Aponte o raio para uma peça
- Clique no gatilho (o raycast detecta a peça)
- As casas válidas aparecem em destaque
- Aponte para a casa de destino e clique novamente

---

## 🎮 Controles Gerais

### Standalone Mobile & AR
- **Toque na peça** → Seleciona e mostra movimentos válidos
- **Toque na casa destacada** → Move a peça
- **Toque fora** → Deseleciona

### VR
- **Apontar raio + Gatilho na peça** → Seleciona e mostra movimentos válidos
- **Apontar raio + Gatilho na casa** → Move a peça
- **Grip no tabuleiro** → Pega e gira o tabuleiro todo

---

## 🔧 Troubleshooting

### AR: "Marcador não detecta"
- ✅ Reference Image Library configurada corretamente?
- ✅ ARTrackedImageManager tem a library atribuída?
- ✅ Marcador impresso em tamanho adequado (15cm+)?
- ✅ Boa iluminação no ambiente?
- ✅ Build para dispositivo real (não funciona no editor)?

### VR: "Não consigo pegar o tabuleiro"
- ✅ BoardManager tem Rigidbody?
- ✅ BoardManager tem Collider grande o suficiente?
- ✅ XRGrabInteractable foi adicionado (deve acontecer automaticamente)?
- ✅ Controllers têm XR Ray Interactor ou Direct Interactor?

### Geral: "Não detecta cliques/toques"
- ✅ Camera atribuída ou MainCamera tag configurada?
- ✅ Active Input Handling = Input System Package (New)?
- ✅ Peças têm colisores (pode ser em filhos)?
- ✅ Não há UI bloqueando raycasts na frente?

---

## 📝 Notas Adicionais

- **Modo pode ser trocado no Inspector** sem recompilar
- **Tilt só funciona em Standalone Mobile**
- **AR e VR desabilitam tilt automaticamente**
- **VR grab pode ser desabilitado** com `Enable VR Grab = false`
- **Target Image Name** deve corresponder exatamente ao nome na Reference Image Library

---

## 🚀 Próximos Passos

Se quiser expandir:
- **En passant e promoção** do peão
- **Roque** do rei
- **Validação de xeque** (impedir movimentos que deixam o rei em xeque)
- **UI para escolher promoção** (rainha, torre, bispo, cavalo)
- **Multiplayer** via Photon/Mirror/Netcode
- **IA para jogar contra o computador**

---

Qualquer dúvida, só perguntar! 🎯
