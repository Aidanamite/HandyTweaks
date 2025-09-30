using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class ScrollOnSelect : MonoBehaviour, ISelectHandler
    {
        public bool includeMouseSelect = false;

        public void OnSelect(BaseEventData data)
        {
            if (!includeMouseSelect && data?.currentInputModule?.input != null && (data.currentInputModule.input.GetMouseButtonUp(0) || data.currentInputModule.input.GetMouseButtonDown(0)))
                return;
            var scroll = GetComponentInParent<ScrollRect>();
            var target = data.selectedObject?.GetComponent<RectTransform>();
            if (scroll && target)
                ScrollTo(scroll, target);
        }

        public static void ScrollTo(ScrollRect scroll, RectTransform selected)
        {
            var viewport = scroll.viewport;
            if (!viewport)
                viewport = (RectTransform)scroll.content.parent;
            var contentArea = scroll.content.rect;
            var targetArea = selected.rect;
            var viewportArea = viewport.rect;
            var localMin = (Vector2)scroll.content.InverseTransformPoint(selected.TransformPoint(targetArea.min)) - contentArea.min;
            var localMax = (Vector2)scroll.content.InverseTransformPoint(selected.TransformPoint(targetArea.max)) - contentArea.min;
            var viewMin = (Vector2)scroll.content.InverseTransformPoint(viewport.TransformPoint(viewportArea.min)) - contentArea.min;
            var viewMax = (Vector2)scroll.content.InverseTransformPoint(viewport.TransformPoint(viewportArea.max)) - contentArea.min;
            var viewportSize = viewMax - viewMin;
            var contentSize = contentArea.size;
            for (int i = 0; i <= 1; i++)
                if (i == 0 ? scroll.horizontal : scroll.vertical)
                {
                    float result;
                    if (localMin[i] < viewMin[i])
                        result = localMin[i] / (contentSize[i] - viewportSize[i]);
                    else if (localMax[i] > viewMax[i])
                        result = (localMax[i] - viewportSize[i]) / (contentSize[i] - viewportSize[i]);
                    else
                        continue;
                    if (i == 0)
                        scroll.horizontalNormalizedPosition = Mathf.Clamp01(result);
                    else
                        scroll.verticalNormalizedPosition = Mathf.Clamp01(result);
                }
        }
    }
}
