using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace HandyTweaks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
#if GAME
    [RequireComponent(typeof(KAUI))]
#endif
    public class HandyUI : MonoBehaviour
    {
        CanvasGroup _group;
        CanvasGroup group => !_group ? _group = GetComponent<CanvasGroup>() : _group;
        public virtual void Awake()
        {
        }
        protected KAUI handle;
        public virtual bool interactable { get => group.interactable; set => group.interactable = value; }

        public HandyUI OpenInstance() => HandyUIManager.OpenUI(this);

        protected virtual void OnEnable()
        {
            HandyUIManager.NotifyOpen(this);
        }
        protected virtual void OnDisable()
        {
            HandyUIManager.Close(this);
            KAUI.RemoveExclusive(handle);
        }
        public virtual void OnCloseButton()
        {
            if (interactable)
                Close();
        }
        public virtual void Close()
        {
            Destroy(gameObject);
        }

        public virtual void BringToFront()
        {
            HandyUIManager.NotifyOpen(this, true);
        }
    }
}
