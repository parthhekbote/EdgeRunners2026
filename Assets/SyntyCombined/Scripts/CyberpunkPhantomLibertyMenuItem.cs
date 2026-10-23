using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SyntyCombined
{
    /// <summary>
    /// Authentic Cyberpunk 2077: Phantom Liberty Menu Item Interactive Controller.
    /// Manages the distinct yellow rectangular selection box, electric cyan hand pointer cursor,
    /// protocol tech badge, and rich audio-visual feedback.
    /// </summary>
    public class CyberpunkPhantomLibertyMenuItem : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler,
        IPointerDownHandler
    {
        public TMP_Text labelText;
        public GameObject selectedBox;
        public InGamePauseManager pauseMgr;

        public static readonly Color YellowActive = new Color(0.988f, 0.933f, 0.039f, 1.0f); // #FCEE0A
        public static readonly Color CrimsonNormal = new Color(0.85f, 0.22f, 0.22f, 0.90f);

        public void SetSelected(bool selected)
        {
            if (selectedBox != null) selectedBox.SetActive(selected);
            if (labelText != null) labelText.color = selected ? YellowActive : CrimsonNormal;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            DeselectSiblings();
            SetSelected(true);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
            if (pauseMgr != null) pauseMgr.PlayHoverAudio();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Do not immediately turn off if mouse leaves without entering another item,
            // preserving the authentic CP2077 active cursor focus.
        }

        public void OnSelect(BaseEventData eventData)
        {
            DeselectSiblings();
            SetSelected(true);
            if (pauseMgr != null) pauseMgr.PlayHoverAudio();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetSelected(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pauseMgr != null) pauseMgr.PlayClickAudio();
        }

        private void DeselectSiblings()
        {
            if (transform.parent != null)
            {
                var siblings = transform.parent.GetComponentsInChildren<CyberpunkPhantomLibertyMenuItem>();
                foreach (var s in siblings)
                {
                    if (s != this) s.SetSelected(false);
                }
            }
        }
    }
}
