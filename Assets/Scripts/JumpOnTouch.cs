using UnityEngine;
using Vuforia;
using UnityEngine.InputSystem;

public class JumpOnTouch : MonoBehaviour
{
	[Header("Jump Settings")]
	[SerializeField] private float jumpHeight = 1f;
	[SerializeField] private float jumpDuration = 0.5f;
	
	private Vector3 originalPosition;
	private bool isJumping = false;
	private float jumpTimer = 0f;
	private Camera arCamera;
	
	private void Start()
	{
		originalPosition = transform.position;

		// Find the camera (Vuforia or Main Camera)
		arCamera = Camera.main;
		if (arCamera == null)
		{
			// Try to find Vuforia Camera if Camera.main is not set
			var vuforiaBehaviour = FindFirstObjectByType<VuforiaBehaviour>();
			if (vuforiaBehaviour != null)
			{
				arCamera = vuforiaBehaviour.GetComponent<Camera>();
			}
		}
		
		if (arCamera == null)
		{
			// Fallback to any camera
			arCamera = FindFirstObjectByType<Camera>();
		}
		
		// Check for required components
		if (GetComponent<Collider>() == null)
		{
			Debug.LogError($"JumpOnTouch: {gameObject.name} needs a Collider component to detect clicks!");
		}
	}
	
	private void Update()
	{
		// Handle touch/click input using the new Input System
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
				Debug.LogError("JumpOnTouch: No camera found!");
				return;
			}
			
			Ray ray = arCamera.ScreenPointToRay(inputPosition);
			RaycastHit hit;
			
			if (Physics.Raycast(ray, out hit, Mathf.Infinity))
			{
				if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(gameObject.transform))
				{
					Jump();
				}
			}
		}
		
		// Handle jump animation
		if (isJumping)
		{
			jumpTimer += Time.deltaTime;
			float progress = jumpTimer / jumpDuration;
			
			if (progress >= 1f)
			{
				transform.position = originalPosition;
				isJumping = false;
				jumpTimer = 0f;
			}
			else
			{
				// Parabolic jump using sine wave
				float height = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
				transform.position = originalPosition + Vector3.up * height;
			}
		}
	}
	
	public void Jump()
	{
		if (!isJumping)
		{
			originalPosition = transform.position;
			isJumping = true;
			jumpTimer = 0f;
		}
	}
}
