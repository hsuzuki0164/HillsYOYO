using UnityEngine;

public class InputManager : MonoBehaviour
{
    public GameManager gameManager;

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // ç∂ÉNÉäÉbÉN
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            gameManager.CheckHit(mousePosition.x, mousePosition.y);
        }
    }
}
