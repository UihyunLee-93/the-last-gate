using UnityEngine;

public class Gate : MonoBehaviour
{
    public int maxHP = 100;
    public int currentHP;

    void Awake()
    {
        currentHP = maxHP;
        //gameObject.tag = "Gate";
    }

    public void TakeDamage(int damage)
    {
        currentHP -= damage;
        Debug.Log($"Gate HP: {currentHP}");

        if (currentHP <= 0)
        {
            GameOver();
        }
    }

    void GameOver()
    {
        Debug.Log("💥 GAME OVER - Gate Destroyed");
        Time.timeScale = 0f; // 임시로 게임 멈춤
    }

    void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            TakeDamage(10);
            Destroy(other.gameObject);
        }
    }
}
