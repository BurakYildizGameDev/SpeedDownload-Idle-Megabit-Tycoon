using SpeedDownload.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Tam ekran, gorunmez tiklama yakalayici (GDD Bolum 2: "Oyuncu ekrana dokunur").
    ///
    /// EventSystem uzerinden calisir; boylece proje "Input System (New)" modunda
    /// olmasina ragmen hem fare hem dokunmatik tek kod yoluyla desteklenir.
    /// Kadran katmanlarinin raycastTarget'i kapali oldugu icin kadranin uzerine
    /// yapilan tiklamalar da buraya duser.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class ClickCatcher : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] SpeedController speedController;

        void Start()
        {
            if (speedController == null && GameManager.Instance != null)
                speedController = GameManager.Instance.Speed;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (speedController != null)
            {
                Vector2 pos = eventData != null ? eventData.position : Vector2.zero;
                speedController.RegisterClick(pos);
            }
        }

        public void Bind(SpeedController controller)
        {
            speedController = controller;
        }
    }
}
