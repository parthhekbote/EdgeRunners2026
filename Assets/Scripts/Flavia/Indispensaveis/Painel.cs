using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class Painel : MonoBehaviour
{
    private bool painel = false;
    public GameObject inventoryPainel;
    
    
    
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.T))
        {
            painel = !painel;
            inventoryPainel.SetActive(painel);
        }
        if (painel)
        {
            Cursor.lockState = CursorLockMode.None;
        }    
    }
  

}
