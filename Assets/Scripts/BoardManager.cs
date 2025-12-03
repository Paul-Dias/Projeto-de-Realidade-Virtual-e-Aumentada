using UnityEngine;
using System.Collections.Generic;
using Vuforia;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum PieceType { Peao, Torre, Cavalo, Bispo, Rainha, Rei }
public enum PieceColor { Branco, Preto }
public enum PlayMode { StandaloneMobile, AR_ImageTracking }

public class BoardManager : MonoBehaviour
{
	[Header("Play Mode")]
	[Tooltip("Modo de jogo: Mobile standalone (tilt) ou AR com Image Tracking do Vuforia")]
	[SerializeField] private PlayMode playMode = PlayMode.StandaloneMobile;

	[Header("Game State")]
	private ChessPiece[,] boardState = new ChessPiece[8, 8];
	private GameObject[,] boardTiles = new GameObject[8, 8];
	private ChessPiece selectedPiece;
	private List<GameObject> moveHighlights = new List<GameObject>(); // Para highlights visuais
	public PieceColor currentPlayerColor = PieceColor.Branco;
	public System.Action<PieceColor> OnTurnChanged;

	// Rastreamento do último movimento (para En Passant)
	public ChessPiece lastMovedPiece;
	public int lastMoveFromY;
	public int lastMoveToY;

	[Header("UI")]
	[SerializeField] private GameObject checkmateUI;
	[SerializeField] private Text checkmateLabel;

	[Header("Promotion")]
	[SerializeField] private PromotionUI promotionUI;
	[SerializeField] private GameObject queenPrefab;
	[SerializeField] private GameObject rookPrefab;
	[SerializeField] private GameObject bishopPrefab;
	[SerializeField] private GameObject knightPrefab;
	private bool isPromotionPending = false;

	[Header("Prefabs")]
	[Tooltip("Instancia pai do tabuleiro")]
	[SerializeField] private GameObject boardParent;

	[SerializeField] private GameObject highlightPrefab; // Prefab para indicar casas válidas

	[Header("Configurações do Tabuleiro")]
	[Tooltip("Tamanho de cada casa do tabuleiro.")]
	[SerializeField] private float tileSize = 1.0f; // Tamanho de cada casa do tabuleiro
	[SerializeField] private bool enableDebugLogs = false;

	[Header("Animação de Movimento")]
	[Tooltip("Duração da animação de movimento das peças (em segundos)")]
	[SerializeField] private float moveDuration = 0.5f;
	[Tooltip("Altura do pulo do cavalo")]
	[SerializeField] private float horseJumpHeight = 0.5f;
	private bool isAnimating = false;
	[SerializeField] private Camera arCamera; // Permite atribuir manualmente no Inspector
	// [Tooltip("Offset local (X,Z) do canto inferior esquerdo (0,0) do tabuleiro em relação ao pivot do GameObject do Board.")]
	private Vector2 boardOriginOffsetLocal = Vector2.zero;

	[Header("AR Image Tracking")]
	[Tooltip("Nome do Image Target configurado no Vuforia")]
	[SerializeField] private string targetImageName = "ChessMarker";
	[SerializeField] private ObserverBehaviour imageTargetBehaviour;
	private bool boardPlacedInAR = false;

	[Header("Device Rotation (Tilt) - Apenas Standalone Mobile")]
	[SerializeField] private bool enableDeviceRotation = true; // gira o tabuleiro pelo tilt do celular
	[SerializeField, Range(0f, 90f)] private float maxTiltAngle = 15f; // ângulo máximo de inclinação
	[SerializeField, Range(0.1f, 10f)] private float rotationSensitivity = 1.0f; // sensibilidade
	[SerializeField, Range(0f, 20f)] private float rotationSmoothing = 8f; // suavização (lerp)
	private Quaternion initialBoardRotation;
	private Vector3 accelBaseline = Vector3.zero;
	private bool tiltCalibrated = false;

	void Start()
	{
		// Setup baseado no modo de jogo
		switch (playMode)
		{
			case PlayMode.StandaloneMobile:
				SetupStandaloneMobile();
				break;
			case PlayMode.AR_ImageTracking:
				SetupARImageTracking();
				break;
		}

		boardOriginOffsetLocal = new Vector2(-3.5f * tileSize, -3.5f * tileSize);

		CreateBoard();

		// Resolve AR/Camera reference
		if (!TryResolveCamera(logFound:true))
		{
			Debug.LogError("BoardManager: No camera found! Assign a Camera in the Inspector or ensure your AR Camera is tagged MainCamera.");
		}

		// Guardar rotação inicial do tabuleiro
		initialBoardRotation = transform.rotation;

		// Desativa UI de checkmate no início, se atribuída
		if (checkmateUI != null) checkmateUI.SetActive(false);
	}

	private void SetupStandaloneMobile()
	{
		if (enableDebugLogs) Debug.Log("BoardManager: Standalone Mobile mode - tilt rotation enabled");
	}

	private void SetupARImageTracking()
	{
		// Procurar pelo ImageTarget na cena se não foi atribuído
		if (imageTargetBehaviour == null)
		{
			// Procurar por ImageTargetBehaviour na cena
			var imageTargets = FindObjectsByType<ImageTargetBehaviour>(FindObjectsSortMode.None);
			foreach (var target in imageTargets)
			{
				if (target.TargetName == targetImageName)
				{
					imageTargetBehaviour = target;
					break;
				}
			}
		}

		if (imageTargetBehaviour != null)
		{
			// Registrar diretamente nos eventos do Vuforia
			var observerBehaviour = imageTargetBehaviour.GetComponent<ObserverBehaviour>();
			if (observerBehaviour != null)
			{
				observerBehaviour.OnTargetStatusChanged += OnVuforiaTargetStatusChanged;
			}
			
			if (enableDebugLogs) Debug.Log("BoardManager: AR Image Tracking mode - waiting for Vuforia marker detection");
		}
		else
		{
			Debug.LogError("BoardManager: AR mode requires ImageTargetBehaviour in scene with target name: " + targetImageName);
		}

		// Desabilitar tilt em AR
		enableDeviceRotation = false;
	}

	private void OnVuforiaTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
	{
		// Status.Status indica o estado de tracking
		if (targetStatus.Status == Status.TRACKED || 
			targetStatus.Status == Status.EXTENDED_TRACKED)
		{
			// Marcador detectado e sendo rastreado
			OnVuforiaTargetFound();
		}
		else if (targetStatus.Status == Status.NO_POSE)
		{
			// Marcador perdido
			OnVuforiaTargetLost();
		}
	}

	// Callbacks do Vuforia
	private void OnVuforiaTargetFound()
	{
		if (!boardPlacedInAR && imageTargetBehaviour != null)
		{
			// Posicionar tabuleiro sobre o marcador
			transform.SetParent(imageTargetBehaviour.transform);
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			
			boardPlacedInAR = true;

			if (enableDebugLogs) Debug.Log($"BoardManager: Board placed on Vuforia marker '{targetImageName}'");
		}
	}

	private void OnVuforiaTargetLost()
	{
		if (enableDebugLogs) Debug.Log($"BoardManager: Vuforia marker '{targetImageName}' lost");
		// Opcional: você pode ocultar o tabuleiro ou pausar o jogo
	}

	// Use essa função para lidar com o input de mouse ou touch
	public void HandleSquareSelection(Vector3 worldPosition)
	{
		// Converte a posição do clique/toque no MUNDO para coordenadas LOCAIS do tabuleiro
		Vector3 local = transform.InverseTransformPoint(worldPosition);
		local.x -= boardOriginOffsetLocal.x;
		local.z -= boardOriginOffsetLocal.y;
		int x = Mathf.FloorToInt(local.x / tileSize);
		int y = Mathf.FloorToInt(local.z / tileSize);

		if (x < 0 || x >= 8 || y < 0 || y >= 8) {
			Debug.LogWarning($"HandleSquareSelection: Coordinates out of bounds ({x},{y})");
			return;
		}

		ChessPiece pieceAtSelection = GetPieceAt(x, y);
		if (pieceAtSelection != null)
		{
			HandleSquareSelection(pieceAtSelection);
			return;
		}
	}
	public void HandleSquareSelection(ChessPiece piece)
	{

		int x = piece.currentX;
		int y = piece.currentY;

		// Garante que as coordenadas estão dentro do tabuleiro
		if (x < 0 || x >= 8 || y < 0 || y >= 8) {
			Debug.LogWarning($"HandleSquareSelection: Coordinates out of bounds ({x},{y})");
			return;
		}

		if (selectedPiece == null)
		{
			// Se nenhuma peça estiver selecionada, seleciona a peça clicada
			if (piece != null)
			{
				if (piece.color == currentPlayerColor)
				{
					Debug.Log("BoardManager: No piece selected, selecting clicked piece.");
					SelectPiece(piece);
				}
			}
		}
		else
		{
			if (piece != null)
			{
				if (piece.color == selectedPiece.color)
				{
					// Seleciona uma nova peça da mesma cor
					SelectPiece(piece);
				}
				else if (selectedPiece == piece)
				{
					// Deseleciona a peça
					DeselectPiece();
				}
				else
				{
					// Tenta mover para capturar a peça inimiga
					MoveSelectedPiece(x, y);
				}
			}
			else
			{
				// Se uma peça já estiver selecionada, tenta mover para a casa clicada
				MoveSelectedPiece(x, y);
			}
		}
	}

	#region Private Methods

	private void CreateBoard()
	{
		if (boardParent == null)
		{
			// enviando um aviso na tela do jogador e fechando o jogo
			Debug.LogError("BoardManager: boardParent is not assigned in the Inspector!");
			return;
		}

		foreach (Transform row in boardParent.GetComponentsInChildren<Transform>())
		{
			if (row.parent != boardParent.transform) continue; // só linhas diretas

			foreach (Transform tile in row.GetComponentsInChildren<Transform>())
			{
				if (tile.parent != row) continue; // só tiles diretas

				// Calcula coordenadas x,y baseadas na hierarquia
				int x = tile.GetSiblingIndex();
				int y = row.GetSiblingIndex();

				boardTiles[x, y] = tile.gameObject;
				ChessPiece piece = tile.GetComponentInChildren<ChessPiece>();
				if (piece != null)
				{
					piece.Setup(x, y, this);
					boardState[x, y] = piece;
					if (enableDebugLogs)
					{
						Debug.Log($"BoardManager: Registered piece '{piece.color}' '{piece.type}' at ({x},{y})");
					}
				}
			}
		}
	}

	private void SelectPiece(ChessPiece piece)
	{
		// Lógica de turno aqui (ex: if (piece.color != currentPlayerColor) return;)

		selectedPiece = piece;

		// Limpa highlights antigos e mostra os novos
		ClearHighlights();
		bool[,] validMoves = selectedPiece.GetValidMoves();
		ShowValidMoves(validMoves);

		if (enableDebugLogs)
		{
			Debug.Log($"Selected piece: {piece.type} at ({piece.currentX},{piece.currentY})");
		}
	}

	private void DeselectPiece()
	{
		selectedPiece = null;
		ClearHighlights();
	}

	private bool CanMakeMove(int toX, int toY)
	{
		if (selectedPiece == null) return false;

		bool[,] validMoves = selectedPiece.GetValidMoves();
		if (!validMoves[toX, toY]) return false;

		// Bloqueia movimentos que deixam o rei em cheque
		if (WouldLeaveKingInCheck(selectedPiece, toX, toY)) return false;
		return true;
	}

	private void MoveSelectedPiece(int targetX, int targetY)
	{
		if (selectedPiece == null) return;
		if (isAnimating) return; // Não permite novo movimento durante animação
		
		if (!CanMakeMove(targetX, targetY)) return;
		
		// Executa o movimento
		ExecuteMove(selectedPiece, targetX, targetY);
		
		// Troca de turno (agora será chamado após a animação)
		StartCoroutine(ToggleTurnAfterAnimation());
	}

	// Método público para IA ou outros sistemas moverem uma peça diretamente
	public bool TryMakeMove(int fromX, int fromY, int toX, int toY)
	{
		if (isAnimating) return false; // Não permite movimento durante animação
		
		ChessPiece piece = GetPieceAt(fromX, fromY);
		if (piece == null) return false;
		if (piece.color != currentPlayerColor) return false;
		
		bool[,] validMoves = piece.GetValidMoves();
		if (!validMoves[toX, toY]) return false;
		
		ExecuteMove(piece, toX, toY);
		StartCoroutine(ToggleTurnAfterAnimation());
		return true;
	}

	private void ExecuteMove(ChessPiece piece, int targetX, int targetY)
	{
		// Inicia a animação de movimento
		StartCoroutine(AnimateMove(piece, targetX, targetY));
	}

	private System.Collections.IEnumerator AnimateMove(ChessPiece piece, int targetX, int targetY)
	{
		isAnimating = true;

		int startX = piece.currentX;
		int startY = piece.currentY;


		// Captura a peça inimiga se existir
		ChessPiece pieceToCapture = GetPieceAt(targetX, targetY);
		// En Passant: peão move em diagonal para casa vazia e captura peão atrás
		if (piece.type == PieceType.Peao && pieceToCapture == null && targetX != startX)
		{
			int capturedY = startY + ((piece.color == PieceColor.Branco) ? 1 : -1);
			ChessPiece epPawn = GetPieceAt(targetX, capturedY);
			if (epPawn != null && epPawn.type == PieceType.Peao && epPawn.color != piece.color)
			{
				pieceToCapture = epPawn;
				boardState[targetX, capturedY] = null;
				Destroy(epPawn.gameObject);
			}
		}

		if (pieceToCapture != null)
		{
			Destroy(pieceToCapture.gameObject);
		}

		// Posições inicial e final
		Vector3 startPos = piece.transform.position;
		Vector3 endPos = GetWorldPosition(targetX, targetY);

		// Determina se é um cavalo para fazer animação de pulo
		bool isHorse = (piece.type == PieceType.Cavalo);

		// Anima o movimento
		float elapsed = 0f;
		while (elapsed < moveDuration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / moveDuration;

			if (isHorse)
			{
				// Cavalo: movimento com pulo (parábola)
				Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
				// Adiciona altura parabólica (sin para criar arco)
				float height = Mathf.Sin(t * Mathf.PI) * horseJumpHeight;
				currentPos.y = startPos.y + height;
				piece.transform.position = currentPos;
			}
			else
			{
				// Outras peças: movimento arrastado (lerp suave)
				piece.transform.position = Vector3.Lerp(startPos, endPos, t);
			}

			yield return null;
		}

		// Garante que a peça chegue exatamente na posição final
		piece.transform.position = endPos;

		// Atualiza a matriz do estado do jogo
		boardState[piece.currentX, piece.currentY] = null;
		piece.currentX = targetX;
		piece.currentY = targetY;
		boardState[targetX, targetY] = piece;

		// Incrementa contador de movimentos
		piece.moveCount++;

		// Atualiza o parent para o tile de destino
		GameObject tile = boardTiles[targetX, targetY];
		piece.transform.SetParent(tile.transform);

		// Roque (mover torre ao mover o rei 2 casas)
		if (piece.type == PieceType.Rei && Mathf.Abs(targetX - startX) == 2)
		{
			int kingY = targetY;
			if (targetX == 6)
			{
				// Roque pequeno: mover torre da coluna 7 para 5
				ChessPiece rook = GetPieceAt(7, kingY);
				if (rook != null && rook.type == PieceType.Torre)
				{
					boardState[7, kingY] = null;
					boardState[5, kingY] = rook;
					rook.currentX = 5;
					rook.currentY = kingY;
					rook.moveCount++;
					// Anima a torre para a nova posição sincronizada
					Vector3 rookStart = rook.transform.position;
					Vector3 rookEnd = GetWorldPosition(5, kingY);
					StartCoroutine(AnimateAuxiliaryPiece(rook, rookStart, rookEnd));
				}
			}
			else if (targetX == 2)
			{
				// Roque grande: mover torre da coluna 0 para 3
				ChessPiece rook = GetPieceAt(0, kingY);
				if (rook != null && rook.type == PieceType.Torre)
				{
					boardState[0, kingY] = null;
					boardState[3, kingY] = rook;
					rook.currentX = 3;
					rook.currentY = kingY;
					rook.moveCount++;
					// Anima a torre para a nova posição sincronizada
					Vector3 rookStart = rook.transform.position;
					Vector3 rookEnd = GetWorldPosition(3, kingY);
					StartCoroutine(AnimateAuxiliaryPiece(rook, rookStart, rookEnd));
				}
			}
		}

		// Registrar último movimento (para en passant)
		lastMovedPiece = piece;
		lastMoveFromY = startY;
		lastMoveToY = targetY;

		// Promoção de peão: se atingir a última fileira
		if (piece.type == PieceType.Peao)
		{
			bool reachedLastRank = (piece.color == PieceColor.Branco && targetY == 7) || (piece.color == PieceColor.Preto && targetY == 0);
			if (reachedLastRank)
			{
				isPromotionPending = true;
				// Mostrar UI se disponível, senão promover automaticamente para Rainha
				if (promotionUI != null)
				{
					promotionUI.Setup((selectedType) => {
						PromotePawnTo(piece, selectedType);
						isPromotionPending = false;
					});
				}
				else
				{
					PromotePawnTo(piece, PieceType.Rainha);
					isPromotionPending = false;
				}
			}
		}

		// Limpa seleção e highlights
		selectedPiece = null;
		ClearHighlights();

		isAnimating = false;
	}

	private System.Collections.IEnumerator AnimateAuxiliaryPiece(ChessPiece aux, Vector3 startPos, Vector3 endPos)
	{
		float elapsed = 0f;
		while (elapsed < moveDuration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / moveDuration;
			aux.transform.position = Vector3.Lerp(startPos, endPos, t);
			yield return null;
		}
		aux.transform.position = endPos;
		GameObject tile = boardTiles[aux.currentX, aux.currentY];
		aux.transform.SetParent(tile.transform);
	}

	private void ToggleTurn()
	{
		currentPlayerColor = (currentPlayerColor == PieceColor.Branco) ? PieceColor.Preto : PieceColor.Branco;
		OnTurnChanged?.Invoke(currentPlayerColor);

		// Verifica checkmate para o próximo jogador
		CheckForCheckmate(currentPlayerColor);
	}

	private System.Collections.IEnumerator ToggleTurnAfterAnimation()
	{
		// Aguarda a animação terminar
		while (isAnimating)
		{
			yield return null;
		}
		// Aguarda promoção, se estiver pendente
		while (isPromotionPending)
		{
			yield return null;
		}
		ToggleTurn();
	}

	private void PromotePawnTo(ChessPiece pawn, PieceType toType)
	{
		// Determina prefab
		GameObject prefab = null;
		switch (toType)
		{
			case PieceType.Rainha: prefab = queenPrefab; break;
			case PieceType.Torre: prefab = rookPrefab; break;
			case PieceType.Bispo: prefab = bishopPrefab; break;
			case PieceType.Cavalo: prefab = knightPrefab; break;
			default: prefab = queenPrefab; break;
		}

		int x = pawn.currentX;
		int y = pawn.currentY;
		GameObject tile = boardTiles[x, y];

		// Remove o peão visualmente
		Destroy(pawn.gameObject);

		// Instancia nova peça
		if (prefab != null)
		{
			GameObject newPieceGO = Instantiate(prefab, GetWorldPosition(x, y), Quaternion.identity, tile.transform);
			ChessPiece newPiece = newPieceGO.GetComponent<ChessPiece>();
			newPiece.color = pawn.color;
			newPiece.type = toType;
			newPiece.Setup(x, y, this);
			newPiece.moveCount = 0;
			boardState[x, y] = newPiece;
		}
		else
		{
			// Sem prefab: apenas atualiza o estado para rainha sem trocar modelo (fallback)
			pawn.type = toType;
			boardState[x, y] = pawn;
		}
	}

	private void ShowValidMoves(bool[,] moves)
	{
		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				if (moves[i, j])
				{
					if (selectedPiece != null && WouldLeaveKingInCheck(selectedPiece, i, j))
						continue;
					GameObject tile = boardTiles[i, j];
					GameObject go = Instantiate(highlightPrefab, GetWorldPosition(i, j), Quaternion.identity, transform);

					// Adiciona ou obtém o componente HighlightSquare
					HighlightSquare highlightScript = go.GetComponent<HighlightSquare>();
					if (highlightScript == null)
					{
						highlightScript = go.AddComponent<HighlightSquare>();
					}

					// Configura as coordenadas do destino
					highlightScript.Setup(i, j, this);

					moveHighlights.Add(go);

					go.transform.SetParent(tile.transform);
				}
			}
		}
	}

	public void Update()
	{
		bool inputDetected = false;
		Vector2 inputPosition = Vector2.zero;

		// Touch input (mobile)
		if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
		{
			inputDetected = true;
			inputPosition = Touchscreen.current.primaryTouch.position.ReadValue();
		}
		// Mouse input (Editor/Standalone)
		else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			inputDetected = true;
			inputPosition = Mouse.current.position.ReadValue();
		}

		if (inputDetected)
		{
			if (arCamera == null)
			{
				// Attempt late binding in case camera was instantiated after Start
				if (!TryResolveCamera(logFound:false))
				{
					return; // silently ignore this frame
				}
			}

			Ray ray = arCamera.ScreenPointToRay(inputPosition);
			RaycastHit hit;

			if (enableDebugLogs)
			{
				Debug.Log($"Input detected at {inputPosition}. Casting ray from camera.");
			}

			if (Physics.Raycast(ray, out hit, Mathf.Infinity))
			{
				Debug.Log($"Raycast hit: {hit.collider.gameObject.name}");

				// Se objeto for um HighlightSquare, lida com a seleção diretamente
				if (hit.collider.gameObject.GetComponent<HighlightSquare>() != null)
				{
					try {
						HighlightSquare highlight = hit.collider.gameObject.GetComponent<HighlightSquare>();
						OnHighlightClicked(highlight.targetX, highlight.targetY);
						if (enableDebugLogs)
						{
							Debug.Log($"HighlightSquare selected at ({highlight.targetX},{highlight.targetY})");
						}
						return;
					} catch (System.Exception ex)
					{
						Debug.LogError($"Error handling HighlightSquare selection: {ex.Message}");
					}
				}

				// Verificando se o objeto clicado é uma peça ou parte dela
				try
				{
					ChessPiece piece = hit.collider.transform.GetComponentInParent<ChessPiece>();
					// Se for uma peça, tenta selecionar a peça
					if (piece != null)
					{
						HandleSquareSelection(piece);
						if (enableDebugLogs)
						{
							Debug.Log($"Piece selected via raycast: {piece.type}-{piece.color} at ({piece.currentX},{piece.currentY})");
						}
					}
					else
					{
						// Se não for uma peça, tenta selecionar a casa do tabuleiro
						HandleSquareSelection(hit.point);
					}
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"Error handling selection: {ex.Message}");
				}
			}
			else if (enableDebugLogs)
			{
				Debug.Log("Raycast hit nothing.");
			}
		}
	}

	// Após lidar com input de toque/clique, processa rotação por tilt
	private void LateUpdate()
	{
		// Tilt só funciona em modo Standalone Mobile
		if (playMode != PlayMode.StandaloneMobile || !enableDeviceRotation) return;

		Vector3 acc;
		if (UnityEngine.InputSystem.Accelerometer.current != null)
		{
			acc = UnityEngine.InputSystem.Accelerometer.current.acceleration.ReadValue();
		}
		else
		{
			// Fallback legacy
			acc = Input.acceleration;
		}

		// Calibração simples: primeira leitura vira baseline
		if (!tiltCalibrated)
		{
			accelBaseline = acc;
			tiltCalibrated = true;
		}

		Vector3 accRelative = acc - accelBaseline;
		// Suavização
		accelBaseline = Vector3.Lerp(accelBaseline, acc, Time.deltaTime * rotationSmoothing);

		// Mapear tilt para ângulos: usar x para rotação em Z e y para rotação em X
		float tiltAroundX = Mathf.Clamp(accRelative.y * 90f, -maxTiltAngle, maxTiltAngle) * rotationSensitivity;
		float tiltAroundZ = Mathf.Clamp(-accRelative.x * 90f, -maxTiltAngle, maxTiltAngle) * rotationSensitivity;

		Quaternion target = Quaternion.Euler(tiltAroundX, 0f, tiltAroundZ) * initialBoardRotation;
		transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotationSmoothing);
	}

	// Permite calibrar o "zero" do tilt através de um botão de UI
	public void CalibrateTiltZero()
	{
		Vector3 acc;
		if (UnityEngine.InputSystem.Accelerometer.current != null)
			acc = UnityEngine.InputSystem.Accelerometer.current.acceleration.ReadValue();
		else
			acc = Input.acceleration;

		accelBaseline = acc;
		initialBoardRotation = transform.rotation;
	}

	private bool TryResolveCamera(bool logFound)
	{
		// 1) Use assigned camera if provided
		if (arCamera != null) return true;

		// 2) Camera.main (tagged MainCamera)
		arCamera = Camera.main;
		if (arCamera != null)
		{
			if (logFound && enableDebugLogs) Debug.Log($"BoardManager: Using Camera.main => {arCamera.name}");
			return true;
		}

		// 3) Vuforia Camera
		var vuforiaBehaviour = FindFirstObjectByType<VuforiaBehaviour>();
		if (vuforiaBehaviour != null)
		{
			arCamera = vuforiaBehaviour.GetComponent<Camera>();
			if (arCamera != null)
			{
				if (logFound && enableDebugLogs) Debug.Log($"BoardManager: Using Vuforia Camera => {arCamera.name}");
				return true;
			}
		}

		// 4) Any enabled camera in the scene
		var cams = Camera.allCameras;
		if (cams != null && cams.Length > 0)
		{
			arCamera = cams[0];
			if (logFound && enableDebugLogs) Debug.Log($"BoardManager: Using first enabled camera => {arCamera.name}");
			return true;
		}

		return false;
	}

	private void OnDestroy()
	{
		// Cleanup dos eventos do Vuforia
		if (imageTargetBehaviour != null)
		{
			var observerBehaviour = imageTargetBehaviour.GetComponent<ObserverBehaviour>();
			if (observerBehaviour != null)
			{
				observerBehaviour.OnTargetStatusChanged -= OnVuforiaTargetStatusChanged;
			}
		}
	}

	private void ClearHighlights()
	{
		foreach (var go in moveHighlights)
		{
			Destroy(go);
		}
		moveHighlights.Clear();
	}

	#endregion

	#region Rules & Validation

	private bool WouldLeaveKingInCheck(ChessPiece piece, int toX, int toY)
	{
		ChessPiece captured = boardState[toX, toY];
		int fromX = piece.currentX;
		int fromY = piece.currentY;

		// En Passant simulation: pawn diagonal into empty square may capture behind
		ChessPiece epCaptured = null;
		int epY = -1;
		if (piece.type == PieceType.Peao && captured == null && toX != fromX)
		{
			epY = fromY + ((piece.color == PieceColor.Branco) ? 1 : -1);
			epCaptured = GetPieceAt(toX, epY);
			if (epCaptured != null && epCaptured.type == PieceType.Peao && epCaptured.color != piece.color)
			{
				boardState[toX, epY] = null;
			}
		}

		boardState[fromX, fromY] = null;
		boardState[toX, toY] = piece;

		int oldX = piece.currentX, oldY = piece.currentY;
		piece.currentX = toX; piece.currentY = toY;

		bool inCheck = IsInCheck(piece.color);

		// revert
		piece.currentX = oldX; piece.currentY = oldY;
		boardState[fromX, fromY] = piece;
		boardState[toX, toY] = captured;
		if (epCaptured != null)
		{
			boardState[toX, epY] = epCaptured;
		}

		return inCheck;
	}

	private bool IsInCheck(PieceColor color)
	{
		int kingX = -1, kingY = -1;
		for (int x = 0; x < 8; x++)
		{
			for (int y = 0; y < 8; y++)
			{
				var p = boardState[x, y];
				if (p != null && p.color == color && p.type == PieceType.Rei)
				{
					kingX = x; kingY = y; break;
				}
			}
			if (kingX != -1) break;
		}
		if (kingX == -1) return false;

		PieceColor enemy = (color == PieceColor.Branco) ? PieceColor.Preto : PieceColor.Branco;
		for (int x = 0; x < 8; x++)
		{
			for (int y = 0; y < 8; y++)
			{
				var p = boardState[x, y];
				if (p != null && p.color == enemy)
				{
					var moves = p.GetValidMoves();
					if (InBounds(kingX, kingY) && moves[kingX, kingY])
						return true;
				}
			}
		}
		return false;
	}

	private bool HasAnyLegalMoves(PieceColor color)
	{
		foreach (var piece in GetPieces(color))
		{
			var moves = piece.GetValidMoves();
			for (int x = 0; x < 8; x++)
			{
				for (int y = 0; y < 8; y++)
				{
					if (moves[x, y] && !WouldLeaveKingInCheck(piece, x, y))
						return true;
				}
			}
		}
		return false;
	}

	private void CheckForCheckmate(PieceColor colorToPlay)
	{
		bool inCheck = IsInCheck(colorToPlay);
		bool hasMoves = HasAnyLegalMoves(colorToPlay);
		if (inCheck && !hasMoves)
		{
			if (checkmateLabel != null)
			{
				PieceColor winner = (colorToPlay == PieceColor.Branco) ? PieceColor.Preto : PieceColor.Branco;
				checkmateLabel.text = $"Xeque-mate! Vencedor: {winner}";
			}
			if (checkmateUI != null) checkmateUI.SetActive(true);
		}
	}

	#endregion

	#region Public Utility Methods

	public ChessPiece GetPieceAt(int x, int y)
	{
		if (x < 0 || x >= 8 || y < 0 || y >= 8) return null;
		return boardState[x, y];
	}

	public Vector3 GetWorldPosition(int x, int y)
	{
		GameObject tile = boardTiles[x, y];
		if (tile != null)
		{
			Vector3 position = tile.transform.position;
			// return new Vector3(position.x, 0f, position.z);
			return position;
		}
		// Converte coordenadas do tabuleiro para posição no MUNDO, respeitando a transformação do tabuleiro (AR/ImageTarget)
		Vector3 local = new Vector3(boardOriginOffsetLocal.x + x * tileSize, 0f, boardOriginOffsetLocal.y + y * tileSize);
		return transform.TransformPoint(local);
	}

	// Helper para IA: retorna todas as peças de uma cor
	public System.Collections.Generic.IEnumerable<ChessPiece> GetPieces(PieceColor color)
	{
		for (int x = 0; x < 8; x++)
		{
			for (int y = 0; y < 8; y++)
			{
				var piece = boardState[x, y];
				if (piece != null && piece.color == color)
				{
					yield return piece;
				}
			}
		}
	}

	// Helper para validar bounds
	public bool InBounds(int x, int y)
	{
		return x >= 0 && x < 8 && y >= 0 && y < 8;
	}

	// Método chamado pelos highlights quando clicados
	public void OnHighlightClicked(int x, int y)
	{
		if (selectedPiece != null && !isAnimating)
		{
			MoveSelectedPiece(x, y);
		}
	}

	#endregion
}