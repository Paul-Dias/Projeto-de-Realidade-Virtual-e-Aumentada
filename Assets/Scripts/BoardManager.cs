using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;

public enum PieceType { Peao, Torre, Cavalo, Bispo, Rainha, Rei }
public enum PieceColor { Branco, Preto }

public class BoardManager : MonoBehaviour
{
	[Header("Game State")]
	private ChessPiece[,] boardState = new ChessPiece[8, 8];
	private ChessPiece selectedPiece;
	private List<GameObject> moveHighlights = new List<GameObject>(); // Para highlights visuais

	[Header("Prefabs")]
	[SerializeField] private GameObject[] whitePiecePrefabs; // Ordem: Peao, Torre, Cavalo, Bispo, Rainha, Rei
	[SerializeField] private GameObject[] blackPiecePrefabs; // Mesma ordem
	[SerializeField] private GameObject highlightPrefab; // Prefab para indicar casas válidas

	[Header("Board Settings")]
	[SerializeField] private float tileSize = 1.0f; // Tamanho de cada casa do tabuleiro
	[SerializeField] private bool enableDebugLogs = false;
	[SerializeField] private Camera arCamera; // Permite atribuir manualmente no Inspector

	[Header("Device Rotation (Tilt)")]
	[SerializeField] private bool enableDeviceRotation = false; // gira o tabuleiro pelo tilt do celular
	[SerializeField, Range(0f, 90f)] private float maxTiltAngle = 15f; // ângulo máximo de inclinação
	[SerializeField, Range(0.1f, 10f)] private float rotationSensitivity = 1.0f; // sensibilidade
	[SerializeField, Range(0f, 20f)] private float rotationSmoothing = 8f; // suavização (lerp)
	private Quaternion initialBoardRotation;
	private Vector3 accelBaseline = Vector3.zero;
	private bool tiltCalibrated = false;

	void Start()
	{
		CreateBoard();

		// Resolve AR/Camera reference
		if (!TryResolveCamera(logFound:true))
		{
			Debug.LogError("BoardManager: No camera found! Assign a Camera in the Inspector or ensure your AR Camera is tagged MainCamera.");
		}

		// Guardar rotação inicial do tabuleiro
		initialBoardRotation = transform.rotation;
		
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
		// Exemplo simples de posicionamento inicial
		// Você pode criar uma lógica mais robusta para isso

		// Peças Brancas
		SpawnPiece(0, 0, PieceType.Torre, PieceColor.Branco);
		SpawnPiece(1, 0, PieceType.Cavalo, PieceColor.Branco);
		SpawnPiece(2, 0, PieceType.Bispo, PieceColor.Branco);
		// ... continue para todas as peças
		for (int i = 0; i < 8; i++)
		{
			SpawnPiece(i, 1, PieceType.Peao, PieceColor.Branco);
		}

		// Peças Pretas
		SpawnPiece(0, 7, PieceType.Torre, PieceColor.Preto);
		SpawnPiece(1, 7, PieceType.Cavalo, PieceColor.Preto);
		// ... continue para todas as peças
		for (int i = 0; i < 8; i++)
		{
			SpawnPiece(i, 6, PieceType.Peao, PieceColor.Preto);
		}
	}

	private void SpawnPiece(int x, int y, PieceType type, PieceColor color)
	{
		GameObject prefab;
		if (color == PieceColor.Branco)
		{
			prefab = whitePiecePrefabs[(int)type];
		}
		else
		{
			prefab = blackPiecePrefabs[(int)type];
		}

		// Instancia o prefab na posição correta do mundo
		GameObject pieceObject = Instantiate(prefab, GetWorldPosition(x, y), Quaternion.identity, transform);
		pieceObject.name = $"{color} {type} ({x},{y})";

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

	private void MoveSelectedPiece(int targetX, int targetY)
	{
		if (selectedPiece == null) return;

		bool[,] validMoves = selectedPiece.GetValidMoves();
		if (validMoves[targetX, targetY])
		{
			// Lógica de Captura: Se houver uma peça na casa de destino, destrua-a
			ChessPiece pieceToCapture = GetPieceAt(targetX, targetY);
			if (pieceToCapture != null)
			{
				Destroy(pieceToCapture.gameObject);
			}

			// Atualiza a matriz do estado do jogo
			boardState[selectedPiece.currentX, selectedPiece.currentY] = null;

			// Move o objeto 3D
			selectedPiece.transform.position = GetWorldPosition(targetX, targetY);

			selectedPiece.currentX = targetX;
			selectedPiece.currentY = targetY;
			boardState[targetX, targetY] = selectedPiece;

			// Desseleciona a peça e limpa os highlights
			selectedPiece = null;
			ClearHighlights();

			// Lógica de fim de turno aqui
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
		if (!enableDeviceRotation) return;

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

		// 4) AR Foundation ARCameraManager on a Camera
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

	#endregion
}