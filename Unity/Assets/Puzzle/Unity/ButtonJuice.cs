using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeclarativePuzzle.Unity
{
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class ButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Tween tween;
        bool hovered;
        void Scale(float value)
        {
            tween?.Kill();
            tween = transform.DOScale(value, .12f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; if (GetComponent<UnityEngine.UI.Button>().IsInteractable()) Scale(1.025f); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Scale(1); }
        public void OnPointerDown(PointerEventData data) { if (GetComponent<UnityEngine.UI.Button>().IsInteractable()) Scale(.965f); }
        public void OnPointerUp(PointerEventData data) => Scale(hovered && GetComponent<UnityEngine.UI.Button>().IsInteractable() ? 1.025f : 1);
        void OnDisable() { tween?.Kill(); transform.localScale = Vector3.one; hovered = false; }
    }
}
