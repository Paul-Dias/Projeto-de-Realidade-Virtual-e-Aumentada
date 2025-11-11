using UnityEngine;
using System.Collections.Generic;
using Vuforia;
using UnityEngine.InputSystem;

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
	private ChessPiece selectedPiece;
	private List<GameObject> moveHighlights = new List<GameObject>(); // Para highlights visuais
	public PieceColor currentPlayerColor = PieceColor.Branco;
	public System.Action<PieceColor> OnTurnChanged;

	[Header("Prefabs")]
	[Tooltip("Instancia pai das peças brancas")]
	[SerializeField] private GameObject whitePiecesParent;
	[Tooltip("Instancia pai das peças pretas")]
	[SerializeField] private GameObject blackPiecesParent;
	[Tooltip("Instancia pai do tabuleiro")]
	[SerializeField] private GameObject boardParent;

	[SerializeField] private GameObject highlightPrefab; // Prefab para indicar casas válidas

	[Header("Configurações do Tabuleiro")]
	[Tooltip("Tamanho de cada casa do tabuleiro.")]
	[SerializeField] private float tileSize = 1.0f; // Tamanho de cada casa do tabuleiro
	[Tooltip("Offset local (X,Z) do canto inferior esquerdo (0,0) do tabuleiro em relação ao pivot do GameObject do Board.")]
	[SerializeField] private Vector2 boardOriginOffsetLocal = Vector2.zero;
	[SerializeField] private bool enableDebugLogs = false;
	[SerializeField] private Camera arCamera; // Permite atribuir manualmente no Inspector

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

		CreateBoard();

		// Resolve AR/Camera reference
		if (!TryResolveCamera(logFound:true))
		{
			Debug.LogError("BoardManager: No camera found! Assign a Camera in the Inspector or ensure your AR Camera is tagged MainCamera.");
		}

		// Guardar rotação inicial do tabuleiro
		initialBoardRotation = transform.rotation;
		
		// Como o tabuleiro e as peças já estão posicionados na cena (AR/ImageTarget),
		// sincronizamos o estado interno do jogo a partir dos objetos existentes.
		SyncBoardFromScene();
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

		// Garante que as coordenadas estão dentro do tabuleiro
		if (x < 0 || x >= 8 || y < 0 || y >= 8) return;

		ChessPiece pieceAtSelection = GetPieceAt(x, y);

		if (selectedPiece == null)
		{
			// Se nenhuma peça estiver selecionada, seleciona a peça clicada
			if (pieceAtSelection != null)
			{
				SelectPiece(pieceAtSelection);
			}
		}
		else
		{
			if (pieceAtSelection != null)
			{
				if (pieceAtSelection.color == selectedPiece.color)
				{
					// Seleciona uma nova peça da mesma cor
					SelectPiece(pieceAtSelection);
				}
				else if (selectedPiece == pieceAtSelection)
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

	/// <summary>
	/// Constrói o estado do tabuleiro (boardState) a partir das peças já existentes na cena.
	/// Útil quando as peças foram posicionadas manualmente como filhos de WhitePieces/BlackPieces.
	/// </summary>
	private void SyncBoardFromScene()
	{
		// Limpa a matriz
		boardState = new ChessPiece[8, 8];

		// Função local para registrar peça
		void RegisterPiece(ChessPiece piece, PieceColor colorHint)
		{
			if (piece == null) return;

			// Calcula (x,y) a partir da posição mundial da peça
			Vector3 local = transform.InverseTransformPoint(piece.transform.position);
			local.x -= boardOriginOffsetLocal.x;
			local.z -= boardOriginOffsetLocal.y;
			int x = Mathf.RoundToInt(local.x / tileSize);
			int y = Mathf.RoundToInt(local.z / tileSize);

			// Garante limites
			if (x < 0 || x > 7 || y < 0 || y > 7)
			{
				if (enableDebugLogs)
					Debug.LogWarning($"SyncBoardFromScene: peça '{piece.name}' fora do tabuleiro em ({x},{y}). Ignorando.");
				return;
			}

			// Define cor se possível pelo parent
			if (whitePiecesParent != null && piece.transform.IsChildOf(whitePiecesParent.transform))
				piece.color = PieceColor.Branco;
			else if (blackPiecesParent != null && piece.transform.IsChildOf(blackPiecesParent.transform))
				piece.color = PieceColor.Preto;
			else
				piece.color = colorHint; // fallback

			// Configura referência ao board e coordenadas internas
			piece.Setup(x, y, this);

			// Registra na matriz
			if (boardState[x, y] != null && boardState[x, y] != piece)
			{
				if (enableDebugLogs)
					Debug.LogWarning($"SyncBoardFromScene: conflito em ({x},{y}). Substituindo '{boardState[x, y].name}' por '{piece.name}'.");
			}
			boardState[x, y] = piece;
		}

		// Percorre filhos de WhitePieces e BlackPieces
		if (whitePiecesParent != null)
		{
			var whitePieces = whitePiecesParent.GetComponentsInChildren<ChessPiece>(includeInactive: false);
			foreach (var p in whitePieces) RegisterPiece(p, PieceColor.Branco);
		}
		if (blackPiecesParent != null)
		{
			var blackPieces = blackPiecesParent.GetComponentsInChildren<ChessPiece>(includeInactive: false);
			foreach (var p in blackPieces) RegisterPiece(p, PieceColor.Preto);
		}

		if (enableDebugLogs) Debug.Log("BoardManager: boardState sincronizado a partir da cena.");
	}

	private void CreateBoard()
	{
		if (boardParent == null)
		{
			// enviando um aviso na tela do jogador e fechando o jogo
			Debug.LogError("BoardManager: boardParent is not assigned in the Inspector!");
			return;
		}

		if (whitePiecesParent == null || blackPiecesParent == null)
		{
			Debug.LogError("BoardManager: whitePiecesParent or blackPiecesParent is not assigned in the Inspector!");
			return;
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
		if (validMoves[toX, toY])
		{
			return true;
		}
		return false;
	}

	private void MoveSelectedPiece(int targetX, int targetY)
	{
		if (selectedPiece == null) return;
		
		if (!CanMakeMove(targetX, targetY)) return;
		
		// Executa o movimento
		ExecuteMove(selectedPiece, targetX, targetY);
		
		// Troca de turno
		ToggleTurn();
	}

	// Método público para IA ou outros sistemas moverem uma peça diretamente
	public bool TryMakeMove(int fromX, int fromY, int toX, int toY)
	{
		ChessPiece piece = GetPieceAt(fromX, fromY);
		if (piece == null) return false;
		if (piece.color != currentPlayerColor) return false;
		
		bool[,] validMoves = piece.GetValidMoves();
		if (!validMoves[toX, toY]) return false;
		
		ExecuteMove(piece, toX, toY);
		ToggleTurn();
		return true;
	}

	private void ExecuteMove(ChessPiece piece, int targetX, int targetY)
	{
		// Captura
		ChessPiece pieceToCapture = GetPieceAt(targetX, targetY);
		if (pieceToCapture != null)
		{
			Destroy(pieceToCapture.gameObject);
		}

		// Atualiza a matriz do estado do jogo
		boardState[piece.currentX, piece.currentY] = null;

		// Move o objeto 3D (usando espaço do tabuleiro)
		piece.transform.position = GetWorldPosition(targetX, targetY);

		piece.currentX = targetX;
		piece.currentY = targetY;
		boardState[targetX, targetY] = piece;

		// Limpa seleção e highlights
		selectedPiece = null;
		ClearHighlights();
	}

	private void ToggleTurn()
	{
		currentPlayerColor = (currentPlayerColor == PieceColor.Branco) ? PieceColor.Preto : PieceColor.Branco;
		OnTurnChanged?.Invoke(currentPlayerColor);
	}

	private void ShowValidMoves(bool[,] moves)
	{
		for (int i = 0; i < 8; i++)
		{
			for (int j = 0; j < 8; j++)
			{
				if (moves[i, j])
				{
					GameObject go = Instantiate(highlightPrefab, GetWorldPosition(i, j), Quaternion.identity, transform);
					moveHighlights.Add(go);
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

				// Verificando se o objeto clicado é uma peça ou parte dela
				try
				{
					ChessPiece piece = hit.collider.transform.GetComponentInParent<ChessPiece>();
					// Se for uma peça, tenta selecionar a peça
					if (piece != null)
					{
						HandleSquareSelection(piece.transform.position);
						if (enableDebugLogs)
						{
							Debug.Log($"Piece selected via raycast: {piece.type} at ({piece.currentX},{piece.currentY})");
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

	#region Public Utility Methods

	public ChessPiece GetPieceAt(int x, int y)
	{
		if (x < 0 || x >= 8 || y < 0 || y >= 8) return null;
		return boardState[x, y];
	}

	public Vector3 GetWorldPosition(int x, int y)
	{
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

	#endregion
}