using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HandyTweaks
{
    public class DragonFilterMenu : MonoBehaviour
    {
        static Transform prefabHolder;
        static DragonFilterMenu prefab;
        static DragonFilterItem prefabFilter;
        static DragonOrderItem prefabOrder;
        public static void SetPrefabs(GameObject main,GameObject filteritem, GameObject orderitem)
        {
            if (prefabHolder)
                Destroy(prefabHolder.gameObject);
            prefabHolder = new GameObject("PrefabHolder").transform;
            prefabHolder.gameObject.SetActive(false);
            DontDestroyOnLoad(prefabHolder.gameObject);
            prefab = Instantiate(main, prefabHolder).AddComponent<DragonFilterMenu>();
            prefabFilter = Instantiate(main, prefabHolder).AddComponent<DragonFilterItem>();
            prefabOrder = Instantiate(main, prefabHolder).AddComponent<DragonOrderItem>();
        }
        public static void Include(string filter)
        {

        }
        public static void Exclude(string filter)
        {

        }
        public static void SetOrder(string order)
        {

        }

        public Button OpenButton;
        public GameObject Menu;
        public Transform FilterGroup;
        public Transform OrderGroup;
        public Button EnableAll;
        public Button DisableAll;

    }

    public class DragonFilterItem : MonoBehaviour
    {
        public Text Label;
        public Image Icon;
        public Toggle OnOff;
        public string Filter;
        void Awake()
        {
            OnOff.onValueChanged.AddListener(OnValueChanged);
            OnValueChanged(OnOff);
        }
        void OnValueChanged(bool value)
        {
            if (value)
                DragonFilterMenu.Include(Filter);
            else
                DragonFilterMenu.Exclude(Filter);
        }
    }

    public class DragonOrderItem : MonoBehaviour, IPointerClickHandler
    {
        public Text Label;
        public Image Ascending;
        public Image Descending;
        public string Order;

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            DragonFilterMenu.SetOrder(Order);
        }
    }
}