using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Moviment : MonoBehaviour
{
    public float speed = 5f;  // Velocidade do jogador

    // Função chamada a cada frame
    void Update()
    {
        // Entrada de movimento usando as teclas W, A, S, D ou as setas do teclado
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        // Vetor de movimento (x = horizontal, y = vertical, z = 0)
        Vector3 movement = new Vector3(moveHorizontal, 0, moveVertical);

        // Move o jogador baseado na velocidade e no tempo
        transform.Translate(movement * speed * Time.deltaTime);
    }
}
