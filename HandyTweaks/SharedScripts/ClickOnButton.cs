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
    public class ClickOnButton : MonoBehaviour
    {
        public string button;
        public bool requireSelected;
        public void Update()
        {
#if GAME
            if (KAInput.GetButtonUp(button) && (!requireSelected || (GetComponent<Selectable>()?.currentSelectionState == Selectable.SelectionState.Selected)) && GetComponentInParent<KAUI>() == KAUI._GlobalExclusiveUI)
                foreach (var click in GetComponents<IPointerClickHandler>())
                    if (!(click is MonoBehaviour) || ((MonoBehaviour)click).enabled)
                    {
                        if (click is Button b)
                            b.OnSubmit(null);
                        else
                            click.OnPointerClick(null);
                    }
#endif
        }
    }
}
