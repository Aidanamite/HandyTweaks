using JSGames.UI;
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
    public class DragonListMenu : MonoBehaviour
    {
        public DragonListItem itemPrefab;
    }

    public class DragonListItem : MonoBehaviour, IPointerClickHandler
    {
        public Text txtName;
        public Text txtAgeSpecies;
        public Image imgIcon;
        public GameObject objLocked;
        public GameObject objBusy;
        public Button btnAgeUp;
        [NonSerialized]
        public RaisedPetData petData;
        public virtual void Init(RaisedPetData pet)
        {
            petData = pet;
            name = petData.Name;
            txtName.text = petData.Name;
            var typeInfo = SanctuaryData.FindSanctuaryPetTypeInfo(petData.PetTypeID);
            if (txtAgeSpecies)
                txtAgeSpecies.text = SanctuaryData.GetDisplayTextFromPetAge(petData.pStage) + " " + typeInfo._NameText.GetLocalizedString();
            if (objLocked)
                objLocked.SetActive(SanctuaryManager.IsPetLocked(petData, "TicketID"));
            int slotIdx = (petData.ImagePosition != null) ? petData.ImagePosition.Value : 0;
            ImageData.Load("EggColor", slotIdx, gameObject);
            bool isBusy = TimedMissionManager.pInstance != null && TimedMissionManager.pInstance.IsPetEngaged(petData.RaisedPetID);
            if (objBusy)
                objBusy.SetActive(isBusy);
            if (btnAgeUp)
                btnAgeUp.gameObject.SetActive(!isBusy && RaisedPetData.GetAgeIndex(petData.pStage) < typeInfo._AgeData.Length - 1);
        }

        public virtual void OnImageLoaded(ImageDataInstance img)
        {
            if (img.mIconTexture == null)
            {
                return;
            }
            if ((petData.ImagePosition ?? 0) == img.mSlotIndex)
                imgIcon.sprite = Sprite.Create(img.mIconTexture, new Rect(0, 0, img.mIconTexture.width, img.mIconTexture.height), new Vector2(0.5f, 0.5f));
        }

        public virtual void OnPointerClick(PointerEventData data)
        {

        }
    }
}
