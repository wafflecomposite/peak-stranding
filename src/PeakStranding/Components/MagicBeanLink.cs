using UnityEngine;

namespace PeakStranding.Components
{
    // Marker placed on the MagicBean object to hold a reference to the spawned vine
    public class MagicBeanLink : MonoBehaviour
    {
        public MagicBeanVine? vine;
        public bool isRestoredBean = false; 

        private void OnDestroy()
        {
            if (isRestoredBean && vine != null)
            {
                Destroy(vine.gameObject);
                vine = null;
            }
        }
    }
}
