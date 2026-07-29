using UnityEngine;
using DG.Tweening;

namespace PPYY.Stage1
{
    public enum EnemyMoveType
    {
        Crow, // カラス：上下左右にランダム移動、速度：中
        Ghost, // ゴースト：波状に移動、速度：低
        Rat,  // ネズミ：直線移動、速度：高
    }

    [RequireComponent(typeof(Collider2D))]
    public class EnemyCharacter : MonoBehaviour, IHittable
    {
        [Header("種別・速度")]
        public EnemyMoveType moveType;
        public float crowSpeed = 3f;
        public float ghostSpeed = 1.5f;
        public float ratSpeed = 5f;

        [Tooltip("この敵の元画像が、無反転(flipX=false)の状態で右向きに描かれている場合はON")]
        public bool baseFacesRight = false;

        [Header("ゴーストの波移動")]
        public float waveAmplitude = 1f;
        public float waveFrequency = 2f;

        [Header("撃破報酬（鍵をランダムでこの範囲だけドロップ）")]
        public int keyRewardMin = 1;
        public int keyRewardMax = 3;

        [Header("ヒット時のフェード消滅")]
        public float fadeOutDuration = 0.4f;

        [Tooltip("ヒット時にランダムで1つ再生するパーティクルプレハブ（複数登録可）")]
        public GameObject[] hitEffectPrefabs;

        [Header("ヒット時の効果音（複数登録するとランダムで再生）")]
        public AudioClip[] hitSounds;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        [Header("移動範囲")]
        public Vector2 boundsMin = new Vector2(-8f, -4f);
        public Vector2 boundsMax = new Vector2(8f, 4f);

        // 破棄されたとき(撃破・強制消去いずれも)に呼ばれる。EnemySpawner が出現数管理に使う
        public event System.Action OnRemoved;

        SpriteRenderer sr;
        Collider2D col;
        bool defeated;

        Vector3 crowTarget;
        Vector3 ratDir;
        Vector3 waveOrigin;
        float waveTime;
        float waveDir = 1f;

        void Awake()
        {
            // アニメーション付きプレハブなど、SpriteRenderer が子オブジェクトにある構成にも対応する
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            SetFacingRight(false); // 初期状態は左向き
        }

        void Start()
        {
            switch (moveType)
            {
                case EnemyMoveType.Crow:
                    PickNewCrowTarget();
                    break;
                case EnemyMoveType.Rat:
                    PickNewRatDirection();
                    break;
                case EnemyMoveType.Ghost:
                    waveOrigin = transform.position;
                    waveDir = Random.value < 0.5f ? -1f : 1f;
                    FaceDirection(waveDir);
                    break;
            }
        }

        void FaceDirection(float dx)
        {
            if (Mathf.Abs(dx) < 0.0001f) return;
            SetFacingRight(dx > 0f);
        }

        // baseFacesRight（元画像が右向きかどうか）を踏まえて、見た目が facingRight を向くように flipX を設定する
        void SetFacingRight(bool facingRight)
        {
            sr.flipX = baseFacesRight ? !facingRight : facingRight;
        }

        void Update()
        {
            if (defeated) return;

            switch (moveType)
            {
                case EnemyMoveType.Crow:
                    MoveCrow();
                    break;
                case EnemyMoveType.Ghost:
                    MoveGhost();
                    break;
                case EnemyMoveType.Rat:
                    MoveRat();
                    break;
            }
        }

        void MoveCrow()
        {
            transform.position = Vector3.MoveTowards(transform.position, crowTarget, crowSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, crowTarget) < 0.1f) PickNewCrowTarget();
        }

        void PickNewCrowTarget()
        {
            crowTarget = new Vector3(
                Random.Range(boundsMin.x, boundsMax.x),
                Random.Range(boundsMin.y, boundsMax.y),
                0);
            FaceDirection(crowTarget.x - transform.position.x);
        }

        void MoveGhost()
        {
            waveTime += Time.deltaTime;
            waveOrigin.x += waveDir * ghostSpeed * Time.deltaTime;

            if (waveOrigin.x > boundsMax.x)
            {
                waveOrigin.x = boundsMax.x;
                waveDir = -1f;
                FaceDirection(waveDir);
            }
            else if (waveOrigin.x < boundsMin.x)
            {
                waveOrigin.x = boundsMin.x;
                waveDir = 1f;
                FaceDirection(waveDir);
            }

            float y = waveOrigin.y + Mathf.Sin(waveTime * waveFrequency) * waveAmplitude;
            transform.position = new Vector3(waveOrigin.x, Mathf.Clamp(y, boundsMin.y, boundsMax.y), 0);
        }

        void MoveRat()
        {
            transform.position += ratDir * ratSpeed * Time.deltaTime;

            if (transform.position.x < boundsMin.x || transform.position.x > boundsMax.x ||
                transform.position.y < boundsMin.y || transform.position.y > boundsMax.y)
            {
                transform.position = new Vector3(
                    Mathf.Clamp(transform.position.x, boundsMin.x, boundsMax.x),
                    Mathf.Clamp(transform.position.y, boundsMin.y, boundsMax.y),
                    0);
                PickNewRatDirection();
            }
        }

        void PickNewRatDirection()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            ratDir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            FaceDirection(ratDir.x);
        }

        void OnDestroy()
        {
            OnRemoved?.Invoke();
        }

        void SpawnHitEffect()
        {
            if (hitEffectPrefabs == null || hitEffectPrefabs.Length == 0) return;
            var effect = hitEffectPrefabs[Random.Range(0, hitEffectPrefabs.Length)];
            if (effect != null) Instantiate(effect, transform.position, Quaternion.identity);
        }

        void PlayHitSound()
        {
            if (hitSounds == null || hitSounds.Length == 0) return;
            var clip = hitSounds[Random.Range(0, hitSounds.Length)];
            SfxPlayer.Play(clip, hitSoundVolume);
        }

        public void OnHit(Vector2 worldPos)
        {
            if (defeated) return;
            defeated = true;
            col.enabled = false;

            PlayerSide side = Stage1ScoreManager.Instance.GetSideFromWorldX(worldPos.x);
            int reward = Random.Range(keyRewardMin, keyRewardMax + 1);
            Stage1ScoreManager.Instance.AddKey(side, reward);

            SpawnHitEffect();
            PlayHitSound();

            transform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 1, 0));
            seq.Append(sr.DOFade(0f, fadeOutDuration));
            seq.OnComplete(() => Destroy(gameObject));
        }
    }
}
