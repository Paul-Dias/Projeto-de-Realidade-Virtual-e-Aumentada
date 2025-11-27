using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Script anexado aos prefabs de highlight para detectar cliques e mover a peça
/// </summary>
public class HighlightSquare : MonoBehaviour
{
	public int targetX;
	public int targetY;
	private BoardManager boardManager;
	private Camera gameCamera;

	public void Setup(int x, int y, BoardManager manager)
	{
		targetX = x;
		targetY = y;
		boardManager = manager;
	}

	private void Start()
	{
		// Tenta encontrar a câmera (mesmo método do BoardManager)
		gameCamera = Camera.main;
		if (gameCamera == null)
		{
			var vuforiaBehaviour = FindFirstObjectByType<Vuforia.VuforiaBehaviour>();
			if (vuforiaBehaviour != null)
			{
				gameCamera = vuforiaBehaviour.GetComponent<Camera>();
			}
		}
		if (gameCamera == null && Camera.allCameras.Length > 0)
		{
			gameCamera = Camera.allCameras[0];
		}
	}

	private void Update()
	{
		if (boardManager == null || gameCamera == null) return;

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
			Ray ray = gameCamera.ScreenPointToRay(inputPosition);
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit, Mathf.Infinity))
			{
				// Verifica se o raycast atingiu este highlight ou um de seus filhos
				if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
				{
					// Notifica o BoardManager para mover a peça
					boardManager.OnHighlightClicked(targetX, targetY);
				}
			}
		}
	}
}
