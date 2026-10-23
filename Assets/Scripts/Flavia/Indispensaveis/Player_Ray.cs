using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using static UnityEditor.Progress;



public class Player_Ray : MonoBehaviour
{



    public Objects[] slots;          
    public Image[] slotImage;        
    public int[] slotAmount;         

    void Start()
    {
        
    }

    
    private void OnTriggerEnter(Collider other)
    {
        
        if (other.CompareTag("Bat4"))
        {
            Destroy(other.gameObject);
            CollectItem(other.gameObject);
        }
    }

    void CollectItem(GameObject item)
    {
        
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].name == item.transform.GetComponent<ObjectType>().objecType.name) 
            {
                slots[i] = item.transform.GetComponent<ObjectType>().objecType; 
                slotImage[i].sprite = slots[i].itemSprite;
                slotAmount[i]++;
                Debug.Log("Item coletado: " + item.name);
                break;
            }
        }
    }

}




