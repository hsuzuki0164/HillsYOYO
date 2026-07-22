using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;




public class GameManager : MonoBehaviour
{

   
    public GameObject whackableObjectPrefab;
    public float minSpawnInterval = 0.5f;
    public float maxSpawnInterval = 2f;
    private float timer;
    public float GameTimer;
    float set_gametime;
    private List<GameObject> whackableObjects;
    [SerializeField] static int score;
    private GameObject score_text;
    private GameObject timer_text;
    private AudioSource bgm;
    private bool bgm_sw = false;
    int state = 0;

    void Start()
    {


        set_gametime = GameTimer;
        ResetTimer();
        whackableObjects = new List<GameObject>();
        score_text = GameObject.Find("Score");
        timer_text = GameObject.Find("Timer");
        bgm=GetComponent<AudioSource>();
      
    }

    void Update()
    {
        switch (state)
        {
            case 0:
                if (Input.GetKey(KeyCode.S))
                {
                    state = 1;
                    GameObject stitle = GameObject.Find("Title");
                    stitle.GetComponent<Text>().enabled = false;
                    GameObject smess = GameObject.Find("Message");
                    smess.GetComponent<Text>().enabled = false; 
                }


                break;
                case 1:

        timer -= Time.deltaTime;
        GameTimer -= Time.deltaTime;

        if (GameTimer <= 0f)
        {
                    state = 2;
        }

        if (timer <= 0f)
        {
            SpawnWhackableObject();
            ResetTimer();
        }
                if (!bgm_sw)
                {
                    bgm.Play();
                    bgm_sw = true;
                }
       
        timer_text.GetComponent<Text>().text = GameTimer.ToString("F2");
        score_text.GetComponent<Text>().text = score.ToString();
                break;
        case 2:
                bgm.Stop();
                GameObject etitle = GameObject.Find("Title");
                etitle.GetComponent<Text>().enabled = true;
                etitle.GetComponent<Text>().text = "終了!";
                GameObject emess = GameObject.Find("Message");
                emess.GetComponent<Text>().enabled = true;
                emess.GetComponent<Text>().text = "Rキーを押してタイトルへ";
                if (Input.GetKey(KeyCode.R))
                {
                    state = 0;
                    etitle.GetComponent<Text>().text = "カラス撃退ゲーム";
                    emess.GetComponent<Text>().text = "Sキーを押すとゲームスタート";
                    GameTimer = set_gametime;
                    bgm_sw = false;
                    score = 0;

                }
                break;

         }

    }

    void SpawnWhackableObject()
    {
        float randomX = Random.Range(-8f, 8f);
        float randomY = Random.Range(-4f, 4f);
        Vector3 spawnPosition = new Vector3(randomX, randomY, 0);
        GameObject newObject = Instantiate(whackableObjectPrefab, spawnPosition, Quaternion.identity);
        whackableObjects.Add(newObject);
    }

    void ResetTimer()
    {
        timer = Random.Range(minSpawnInterval, maxSpawnInterval);
    }

    public void CheckHit(float x, float y)
    {
        for (int i = 0; i < whackableObjects.Count; i++)
        {
            if (whackableObjects[i] != null)
            {
                whackableObjects[i].GetComponent<WhackableObject>().CheckHit(x, y);
            }
        }
    }

    public static void SetScore(int s)
    {
        score += s;

    }


}
