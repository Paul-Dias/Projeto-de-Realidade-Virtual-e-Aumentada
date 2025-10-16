using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Interaction.Toolkit;

public class JumpOnTouch : MonoBehaviour
{
	[Header("Jump Settings")]
	[SerializeField] private float jumpHeight = 1f;
	[SerializeField] private float jumpDuration = 0.5f;
	
	[Header("XR Settings")]
	[SerializeField] private bool useXRInteraction = false;
	
	private Vector3 originalPosition;
	private bool isJumping = false;
	private float jumpTimer = 0f;
	
	private void Start()
	{
		originalPosition = transform.position;
		
		// Add XR Interaction components if needed
		if (useXRInteraction && GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>() == null)
		{
			var interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
			interactable.selectEntered.AddListener(OnXRSelect);
		}
	}
	
	private void Update()
	{
		// Handle mobile AR touch input
		if (!useXRInteraction && Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			
			if (touch.phase == TouchPhase.Began)
			{
				Ray ray = Camera.main.ScreenPointToRay(touch.position);
				RaycastHit hit;
				
				if (Physics.Raycast(ray, out hit))
				{
					if (hit.collider.gameObject == gameObject)
					{
						Jump();
					}
				}
			}
		}
		
		// Handle mouse click for testing in editor
		#if UNITY_EDITOR
		if (Input.GetMouseButtonDown(0))
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;
			
			if (Physics.Raycast(ray, out hit))
			{
				if (hit.collider.gameObject == gameObject)
				{
					Jump();
				}
			}
		}
		#endif
		
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
	
	private void OnXRSelect(SelectEnterEventArgs args)
	{
		Jump();
	}
	
	private void Jump()
	{
		if (!isJumping)
		{
			originalPosition = transform.position;
			isJumping = true;
			jumpTimer = 0f;
		}
	}
	
	private void OnDestroy()
	{
		// Clean up XR events
		var interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
		if (interactable != null)
		{
			interactable.selectEntered.RemoveListener(OnXRSelect);
		}
	}
}