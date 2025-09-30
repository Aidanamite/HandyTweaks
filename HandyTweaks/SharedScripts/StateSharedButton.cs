using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;

namespace HandyTweaks.SharedScripts
{
    public class StateSharedButton : Button
    {
        public int currentState;
#if GAME
        public override void DoStateTransition(SelectionState state, bool instant)
#else
        protected override void DoStateTransition(SelectionState state, bool instant)
#endif
        {
            base.DoStateTransition(state, instant);
            currentState = (int)state;
        }
    }
}
