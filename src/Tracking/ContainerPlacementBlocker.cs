using System.Collections.Generic;
using UnityEngine;

namespace BalancedContainers
{
    public static class ContainerPlacementBlocker
    {
        const float EdgeMargin = 2f;

        public static bool OverlapsAnyBar(
            IReadOnlyList<ContainerHeaderOverlay> overlays,
            Vector2Int candidatePosition,
            Vector2Int candidateSize)
        {
            if (overlays == null || overlays.Count == 0)
            {
                return false;
            }

            if (candidateSize.x <= 0 || candidateSize.y <= 0)
            {
                return false;
            }

            float candMinX = candidatePosition.x + EdgeMargin;
            float candMaxX = candidatePosition.x + candidateSize.x - EdgeMargin;
            float candMinY = candidatePosition.y - candidateSize.y + EdgeMargin;
            float candMaxY = candidatePosition.y - EdgeMargin;

            if (candMaxX <= candMinX || candMaxY <= candMinY)
            {
                candMinX = candidatePosition.x;
                candMaxX = candidatePosition.x + candidateSize.x;
                candMinY = candidatePosition.y - candidateSize.y;
                candMaxY = candidatePosition.y;
            }

            for (int i = 0; i < overlays.Count; i++)
            {
                var overlay = overlays[i];
                if (!overlay || !overlay.TryGetBlockedWorldRect(out var barPos, out var barSize))
                {
                    continue;
                }

                float barMinX = barPos.x;
                float barMaxX = barPos.x + barSize.x;
                float barMinY = barPos.y - barSize.y;
                float barMaxY = barPos.y;

                if (candMinX < barMaxX
                    && candMaxX > barMinX
                    && candMinY < barMaxY
                    && candMaxY > barMinY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

