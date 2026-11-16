using UnityEngine;
using UnityEngine.UI;

namespace BalancedContainers
{
    public sealed class BuildingHighlightPulse : MonoBehaviour
    {
        const float DurationSeconds = 1.35f;

        Image fillImage;
        Image borderImage;
        float elapsed;

        public static void Spawn(Transform containerTransform, Vector2Int containerPos, Vector2Int buildingPos, Vector2 buildingSize)
        {
            if (!containerTransform)
            {
                return;
            }

            float width = Mathf.Max(32f, buildingSize.x);
            float height = Mathf.Max(32f, buildingSize.y);

            var rect = ContainerUiTheme.CreateRect("BalancedContainers_JumpHighlight", containerTransform);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(buildingPos.x - containerPos.x, buildingPos.y - containerPos.y);
            rect.sizeDelta = new Vector2(width, height);
            rect.SetAsLastSibling();

            var pulse = rect.gameObject.AddComponent<BuildingHighlightPulse>();
            pulse.Build(rect);
        }

        void Build(RectTransform rect)
        {
            fillImage = rect.gameObject.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.85f, 0.35f, 0.30f);
            fillImage.raycastTarget = false;

            var borderSprite = ContainerUiTheme.FindFrameSprite();
            if (borderSprite)
            {
                borderImage = ContainerUiTheme.CreateImage(
                    "Border",
                    rect,
                    new Color(1f, 0.86f, 0.32f, 1f),
                    raycastTarget: false);
                borderImage.sprite = borderSprite;
                borderImage.type = Image.Type.Sliced;
                var bRect = borderImage.rectTransform;
                bRect.anchorMin = Vector2.zero;
                bRect.anchorMax = Vector2.one;
                bRect.offsetMin = new Vector2(-4f, -4f);
                bRect.offsetMax = new Vector2(4f, 4f);
            }
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= DurationSeconds)
            {
                Destroy(gameObject);
                return;
            }

            float t = elapsed / DurationSeconds;
            float pulse = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 4f);
            float fade = 1f - t * t;

            if (fillImage)
            {
                fillImage.color = new Color(1f, 0.85f, 0.35f, 0.28f * fade * pulse);
            }

            if (borderImage)
            {
                borderImage.color = new Color(1f, 0.86f, 0.32f, fade * (0.65f + 0.35f * pulse));
            }
        }
    }
}

