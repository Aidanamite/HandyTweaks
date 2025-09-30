using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class DontSelectFromMouse : MonoBehaviour, ISelectHandler, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData data) => OnSelect(data);
        public void OnSelect(BaseEventData data)
        {
            if (GetComponent<Selectable>() && data?.currentInputModule?.input != null && (data.currentInputModule.input.GetMouseButtonUp(0) || data.currentInputModule.input.GetMouseButtonDown(0)))
                GetComponent<Selectable>().OnDeselect(null);
        }
    }
}
