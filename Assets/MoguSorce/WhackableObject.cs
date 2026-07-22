using UnityEngine;
using System.Collections.Generic;
public enum EnemyType
{
    b_clow,
    big_clow,
    w_clow,
}

public class WhackableObject : MonoBehaviour
{
    
    SpriteRenderer spriteRenderer;
    public EnemyType type;
    public Sprite[] sprites = new Sprite[System.Enum.GetValues(typeof(EnemyType)).Length];//キャラ別のスプライト

    public float moveSpeed;//動きの速さ
    public float minX = -8f, maxX = 8f, minY = -4f, maxY = 4f;//移動範囲
    private Vector3 targetPosition;
    private AudioSource Asource;
    public AudioClip Aclip1;
    public AudioClip Aclip2;
    public GameObject effect;
    int score;
    bool hitsw = false;


    void Start()
    {
        int r = 0;
        r= Random.Range(0, 19);
        if (r<10)
        {
            type = EnemyType.b_clow;
            
        }
        else if (r<18)
        {   
            type = EnemyType.big_clow;
           
        }
        else
        {
            type = EnemyType.w_clow;
        }

        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprites[(int)type];
        this.gameObject.name = type.ToString();
        SetNewRandomPosition();
        Asource = this.GetComponent<AudioSource>();
        Asource.PlayOneShot(Aclip1);
        switch ((int)type)
        {
            case 0:
                moveSpeed = 2f;
                score = 100;
                break;
            case 1:
                moveSpeed = 3f;
                score = 300;
                break;
            case 2:
                moveSpeed = 4.5f;
                score = 1000;
                break;

        }


      
    }

    void Update()
    {
        MoveRandomly();
    }

    void MoveRandomly()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * moveSpeed);

        // ターゲット位置に到達した場合、新しいランダム位置を設定
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            SetNewRandomPosition();
        }
    }

    void SetNewRandomPosition()
    {
        targetPosition = new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            0
        );

        // スプライトの反転処理
        if (targetPosition.x < transform.position.x)
        {
            GetComponent<SpriteRenderer>().flipX = true;
        }
        else
        {
            GetComponent<SpriteRenderer>().flipX = false;
        }
    }

    public void CheckHit(float x, float y)//当たり判定のメソッド
    {
        if (!hitsw)
        {
            Vector3 objPosition = transform.position;
            float halfWidth = transform.localScale.x;
            float halfHeight = transform.localScale.y;

            if (x > objPosition.x - halfWidth && x < objPosition.x + halfWidth &&
                y > objPosition.y - halfHeight && y < objPosition.y + halfHeight)
            {
                Destroy(gameObject, 0.3f); // 座標がオブジェクトと重なっている場合にオブジェクトを削除
                Asource.PlayOneShot(Aclip2);
                Vector3 pos = this.GetComponent<Transform>().position;
                GameObject go = Instantiate(effect);
                go.transform.position = pos;
                GameManager.SetScore(score);
                hitsw = true;
            }
        }
    }
}
