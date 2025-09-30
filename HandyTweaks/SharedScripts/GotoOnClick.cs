using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HandyTweaks
{
    public class GotoOnClick : MonoBehaviour, IPointerClickHandler
    {
        public string Level;
        public string Location;
        public void OnPointerClick(PointerEventData data)
        {
            GetComponentInParent<HandyUI>().Close();
            AvAvatar.pStartLocation = Location;
            RsResourceManager.LoadLevel(Level, false);
        }
    }
}
