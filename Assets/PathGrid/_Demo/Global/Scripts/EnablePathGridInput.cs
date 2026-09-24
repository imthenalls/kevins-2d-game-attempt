using TMPro;
using UnityEngine;
using PathGrid.Input;
using UnityEngine.UI;

namespace PathGrid.Example
{
    public class EnablePathGridInput : MonoBehaviour
    {
        public PathGridInputSystem pathGridInputSystem;
        public Text btnText;

        bool isEnabled = false;

        public void EnablePathGrid()
        {
            if (!isEnabled)
            {   
                btnText.text = "Disable Path Grid";
                pathGridInputSystem.EnableInputSystem();
                isEnabled = true;
            }
            else
            {
                btnText.text = "Enable Path Grid";
                pathGridInputSystem.DisableInputSystem();
                isEnabled = false;
            }     
        }        
    }
}