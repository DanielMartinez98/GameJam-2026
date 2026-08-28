using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Scale")]
    public float hoverScale = 1.05f;
    public float clickScale = 0.9f;
    public float clickDuration = 0.1f;

    private Vector3 originalScale;
    private RectTransform rectTransform;

    private bool isHovered;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null)
        {
            Debug.LogError("Hover script requires a RectTransform.");
            enabled = false;
            return;
        }

        originalScale = rectTransform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        rectTransform.localScale = originalScale * hoverScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        rectTransform.localScale = originalScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        rectTransform.localScale = originalScale * clickScale;

        CancelInvoke(nameof(ResetScale));
        Invoke(nameof(ResetScale), clickDuration);
    }

    private void ResetScale()
    {
        if (isHovered)
        {
            rectTransform.localScale = originalScale * hoverScale;
        }
        else
        {
            rectTransform.localScale = originalScale;
        }
    }
}
