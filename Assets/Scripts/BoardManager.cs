using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.InputSystem;

public enum PieceType { Peao, Torre, Cavalo, Bispo, Rainha, Rei }
public enum PieceColor { Branco, Preto }
public enum PlayMode { StandaloneMobile, AR_ImageTracking, VR_Headset }

public class BoardManager : MonoBehaviour
{
	[Header("Play Mode")]
	[Tooltip("Modo de jogo: Mobile standalone (tilt), AR com QR/marcador, ou VR com headset")]
	[SerializeField] private PlayMode playMode = PlayMode.StandaloneMobile;

	[Header("Game State")]
	private ChessPiece[,] boardState = new ChessPiece[8, 8];
	private ChessPiece selectedPiece;
	private List<GameObject> moveHighlights = new List<GameObject>(); // Para highlights visuais
	public PieceColor currentPlayerColor = PieceColor.Branco;
	public System.Action<PieceColor> OnTurnChanged;

	[Header("Prefabs")]
	[SerializeField] private GameObject queenPrefab;
	[SerializeField] private GameObject kingPrefab;
	[SerializeField] private GameObject bishopRightPrefab;
	[SerializeField] private GameObject bishopLeftPrefab;
	[SerializeField] private GameObject knightRightPrefab;
	[SerializeField] private GameObject knightLeftPrefab;
	[SerializeField] private GameObject hookLeftPrefab;
	[SerializeField] private GameObject hookRightPrefab;
	[SerializeField] private GameObject[] pawnPrefabs;
	// Preparando o material para as peças brancas e pretas
	[SerializeField] private Texture2D whitePiecesMaterial;
	[SerializeField] private Texture2D blackPiecesMaterial;

	[SerializeField] private GameObject highlightPrefab; // Prefab para indicar casas válidas
	[SerializeField] private GameObject whiteTilePrefab;
	[SerializeField] private GameObject blackTilePrefab;

	[Header("Configurações do Tabuleiro")]
	[Tooltip("Tamanho de cada casa do tabuleiro.")]
	[SerializeField] private float tileSize = 1.0f; // Tamanho de cada casa do tabuleiro
	[SerializeField] private bool enableDebugLogs = false;
	[SerializeField] private Camera arCamera; // Permite atribuir manualmente no Inspector

	[Header("AR Image Tracking")]
	[Tooltip("Nome da imagem de referência (QR/marcador) na Reference Image Library")]
	[SerializeField] private string targetImageName = "ChessMarker";
	[SerializeField] private ARTrackedImageManager trackedImageManager;
	private ARTrackedImage trackedBoardAnchor;
	private bool boardPlacedInAR = false;

	[Header("VR Interaction")]
	[Tooltip("Permite pegar e girar o tabuleiro no VR")]
	[SerializeField] private bool enableVRGrab = true;
	private XRGrabInteractable grabInteractable;

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
			case PlayMode.VR_Headset:
				SetupVRHeadset();
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
		
	}

	private void SetupStandaloneMobile()
	{
		if (enableDebugLogs) Debug.Log("BoardManager: Standalone Mobile mode - tilt rotation enabled");
	}

	private void SetupARImageTracking()
	{
		if (trackedImageManager == null)
		{
			trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>();
		}

		if (trackedImageManager != null)
		{
			// AR Foundation 6.0: usar trackablesChanged ao invés de trackedImagesChanged
			trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
			if (enableDebugLogs) Debug.Log("BoardManager: AR Image Tracking mode - waiting for marker detection");
		}
		else
		{
			Debug.LogError("BoardManager: AR mode requires ARTrackedImageManager in scene!");
		}

		// Desabilitar tilt em AR
		enableDeviceRotation = false;
	}

	private void SetupVRHeadset()
	{
		// Adicionar XRGrabInteractable para permitir pegar e girar o tabuleiro
		if (enableVRGrab)
		{
			grabInteractable = gameObject.GetComponent<XRGrabInteractable>();
			if (grabInteractable == null)
			{
				grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
			}

			// Configurar para permitir rotação suave
			grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
			grabInteractable.trackRotation = true;
			grabInteractable.throwOnDetach = false;

			if (enableDebugLogs) Debug.Log("BoardManager: VR mode - grab interaction enabled");
		}

		// Desabilitar tilt em VR
		enableDeviceRotation = false;
	}

	// AR Foundation 6.0: usar ARTrackablesChangedEventArgs<ARTrackedImage>
	private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
	{
		// Quando uma imagem é detectada
		foreach (var trackedImage in args.added)
		{
			if (trackedImage.referenceImage.name == targetImageName)
			{
				PlaceBoardOnARMarker(trackedImage);
			}
		}

		// Atualizar posição quando a imagem se move
		foreach (var trackedImage in args.updated)
		{
			if (trackedImage.referenceImage.name == targetImageName && trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking)
			{
				UpdateBoardPositionAR(trackedImage);
			}
		}
	}

	private void PlaceBoardOnARMarker(ARTrackedImage trackedImage)
	{
		trackedBoardAnchor = trackedImage;
		boardPlacedInAR = true;

		// Posicionar tabuleiro sobre o marcador
		transform.position = trackedImage.transform.position;
		transform.rotation = trackedImage.transform.rotation;

		if (enableDebugLogs) Debug.Log($"BoardManager: Board placed on AR marker '{targetImageName}'");
	}

	private void UpdateBoardPositionAR(ARTrackedImage trackedImage)
	{
		if (boardPlacedInAR)
		{
			// Seguir o marcador suavemente
			transform.position = Vector3.Lerp(transform.position, trackedImage.transform.position, Time.deltaTime * 10f);
			transform.rotation = Quaternion.Slerp(transform.rotation, trackedImage.transform.rotation, Time.deltaTime * 10f);
		}
	}

	// Use essa função para lidar com o input, seja ele de mouse ou XR
	public void HandleSquareSelection(Vector3 worldPosition)
	{
		// Converte a posição do clique/toque no mundo para coordenadas do tabuleiro
		int x = Mathf.FloorToInt(worldPosition.x / tileSize);
		int y = Mathf.FloorToInt(worldPosition.z / tileSize);

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
			// Se uma peça já estiver selecionada, tenta mover para a casa clicada
			MoveSelectedPiece(x, y);
		}
	}

	#region Private Methods

	private void CreateBoard()
	{
		// Cria o tabuleiro
		for (int x = 0; x < 8; x++)
		{
			for (int y = 0; y < 8; y++)
			{
				GameObject tilePrefab = ((x + y) % 2 == 0) ? whiteTilePrefab : blackTilePrefab;
				Instantiate(tilePrefab, GetWorldPosition(x, y), Quaternion.identity, transform);
			}
		}

		// Exemplo simples de posicionamento inicial
		// Você pode criar uma lógica mais robusta para isso

		// peças brancas
		SpawnPiece(0, 0, hookLeftPrefab, PieceColor.Branco);
		SpawnPiece(1, 0, knightLeftPrefab, PieceColor.Branco);
		SpawnPiece(2, 0, bishopLeftPrefab, PieceColor.Branco);
		SpawnPiece(3, 0, queenPrefab, PieceColor.Branco);
		SpawnPiece(4, 0, kingPrefab, PieceColor.Branco);
		SpawnPiece(5, 0, bishopRightPrefab, PieceColor.Branco);
		SpawnPiece(6, 0, knightRightPrefab, PieceColor.Branco);
		SpawnPiece(7, 0, hookRightPrefab, PieceColor.Branco);
		for (int i = 0; i < 8; i++)
		{
			SpawnPiece(i, 1, pawnPrefabs[i], PieceColor.Branco);
		}

		// peças pretas
		SpawnPiece(0, 7, hookLeftPrefab, PieceColor.Preto);
		SpawnPiece(1, 7, knightLeftPrefab, PieceColor.Preto);
		SpawnPiece(2, 7, bishopLeftPrefab, PieceColor.Preto);
		SpawnPiece(3, 7, queenPrefab, PieceColor.Preto);
		SpawnPiece(4, 7, kingPrefab, PieceColor.Preto);
		SpawnPiece(5, 7, bishopRightPrefab, PieceColor.Preto);
		SpawnPiece(6, 7, knightRightPrefab, PieceColor.Preto);
		SpawnPiece(7, 7, hookRightPrefab, PieceColor.Preto);
		for (int i = 0; i < 8; i++)
		{
			SpawnPiece(i, 6, pawnPrefabs[i], PieceColor.Preto);
		}
	}

	private void SpawnPiece(int x, int y, GameObject prefab, PieceColor color)
	{
		Texture2D materialToApply = (color == PieceColor.Branco) ? whitePiecesMaterial : blackPiecesMaterial;

		// Instancia o prefab na posição correta do mundo
		GameObject pieceObject = Instantiate(prefab, GetWorldPosition(x, y), Quaternion.identity, transform);
		pieceObject.name = $"{color}_{prefab.name}_({x},{y})";

		ChessPiece pieceComponent = pieceObject.GetComponent<ChessPiece>();
		pieceComponent.Setup(x, y, this); // Configura a peça com suas coordenadas e a referência ao BoardManager

		boardState[x, y] = pieceComponent; // Adiciona a peça ao nosso estado de jogo
	}

	private void SelectPiece(ChessPiece piece)
	{
		// Lógica de turno aqui (ex: if (piece.color != currentPlayerColor) return;)

		selectedPiece = piece;

		// Limpa highlights antigos e mostra os novos
		ClearHighlights();
		bool[,] validMoves = selectedPiece.GetValidMoves();
		ShowValidMoves(validMoves);
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

		// Move o objeto 3D
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

		// 3) XR Origin camera (para VR) - busca pelo componente Camera filho de um objeto com "XR" no nome
		var allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
		foreach (var cam in allCams)
		{
			if (cam.transform.parent != null && (cam.transform.parent.name.Contains("XR") || cam.transform.parent.name.Contains("Origin")))
			{
				arCamera = cam;
				if (logFound && enableDebugLogs) Debug.Log($"BoardManager: Using XR Origin Camera => {arCamera.name}");
				return true;
			}
		}

		// 4) AR Foundation ARCameraManager on a Camera (para AR)
		var arCamMgr = FindFirstObjectByType<UnityEngine.XR.ARFoundation.ARCameraManager>();
		if (arCamMgr != null)
		{
			arCamera = arCamMgr.GetComponent<Camera>();
			if (arCamera != null)
			{
				if (logFound && enableDebugLogs) Debug.Log($"BoardManager: Using Camera with ARCameraManager => {arCamera.name}");
				return true;
			}
		}

		// 5) Any enabled camera in the scene
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
		// Cleanup AR image tracking (AR Foundation 6.0)
		if (trackedImageManager != null)
		{
			trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);
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
		// Converte a coordenada do tabuleiro (ex: 0,0) para uma posição no mundo 3D
		// Isso assume que o seu tabuleiro começa em (0, 0, 0)
		return new Vector3(x * tileSize, 0, y * tileSize);
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