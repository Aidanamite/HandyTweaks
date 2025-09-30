using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class ScrollButton : MonoBehaviour, IPointerClickHandler
    {
        public Scrollbar scrollbar;
        public bool up;
        public void OnPointerClick(PointerEventData evnt)
        {
            scrollbar.OnMove(new AxisEventData(EventSystem.current)
            {
                moveDir = scrollbar.direction <= Scrollbar.Direction.RightToLeft ? (up ? MoveDirection.Right : MoveDirection.Left) : (up ? MoveDirection.Up : MoveDirection.Down),
                moveVector = scrollbar.direction <= Scrollbar.Direction.RightToLeft ? (up ? Vector2.right : Vector2.left) : (up ? Vector2.up : Vector2.down)
            });
        }
    }
}