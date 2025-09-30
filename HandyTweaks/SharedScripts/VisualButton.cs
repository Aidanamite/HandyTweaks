using HandyTweaks.SharedScripts;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class VisualButton : Selectable
    {
        public Selectable Source;
        SelectionState last;
        public override bool IsInteractable()
        {
            return false;
        }
        public override bool IsActive()
        {
            return false;
        }

        bool _transitioning = false;
        protected void DoTransition(SelectionState newState, bool instant)
        {
            _transitioning = true;
            DoStateTransition(newState, instant);
            _transitioning = false;
        }

#if GAME
        public override void DoStateTransition(SelectionState state, bool instant)
#else
        protected override void DoStateTransition(SelectionState state, bool instant)
#endif
        {
            if (_transitioning)
                base.DoStateTransition(state, instant);
        }

#if GAME
        public override void OnEnable()
#else
        protected override void OnEnable()
#endif
        {
            base.OnEnable();
            if (Source)
                DoTransition(last = GetState(Source), true);
        }

        public void Update()
        {
            if (Source && last != GetState(Source))
                DoTransition(last = GetState(Source), false);
        }

#if GAME
        static SelectionState GetState(Selectable selectable) => selectable is StateSharedButton shared ? (SelectionState)shared.currentState : selectable.currentSelectionState;
#else
        static PropertyInfo _state = typeof(Selectable).GetProperty(nameof(currentSelectionState), ~BindingFlags.Default);
        static SelectionState GetState(Selectable selectable) => selectable is StateSharedButton shared ? (SelectionState)shared.currentState : (SelectionState)_state.GetValue(selectable);
#endif
    }
}