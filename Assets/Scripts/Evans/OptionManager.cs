using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NewBehaviourScript : MonoBehaviour
{
    [SerializeField] private string NomeDaCenaDoJogo;
    [SerializeField] private GameObject painelMenuInicial;
    [SerializeField] private GameObject painelOpcoes;
    public void Play()
    {
        SceneManager.LoadScene("Qual a cena?");
    }

    public void OpenOption()
    {
        painelMenuInicial.SetActive(false);
        painelOpcoes.SetActive(true);
    }

    public void CloseOptions()
    {
        painelOpcoes?.SetActive(false);
        painelMenuInicial?.SetActive(true);
    }

    public void LeaveGame()
    {
        Debug.Log("Testagem de saida do jogo");
        Application.Quit();
    }
}


// ADICIONAR CENA NA ABA "NOME DA CENA DO JOGO"