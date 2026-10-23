using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerUIManager : MonoBehaviour
{
    public static PlayerUIManager instance;

    [Header("NETWORK JOIN")]
    [SerializeField] public bool startGameAsClient;

    [HideInInspector] public PlayerUIHudManager playerUIHudManager;

    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        playerUIHudManager = GetComponentInChildren<PlayerUIHudManager>();
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Revise eventually
        //if (startGameAsClient)
        //{
        //    startGameAsClient = false;
        //    // WE MUST FIRST SHUT DOWN BECAUSE WE HAVE STARTED AS A HOST IN THE TITLE SCREEN
        //    NetworkManager.Singleton.Shutdown();
        //    // WE THEN RESTART, AS A CLIENT
        //    NetworkManager.Singleton.StartClient();

        //}
    }
}
