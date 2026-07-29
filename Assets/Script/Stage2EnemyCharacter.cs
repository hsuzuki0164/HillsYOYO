using UnityEngine;
using DG.Tweening;
using PPYY.Stage1; // PlayerSide, IHittable を流用

namespace PPYY.Stage2
{
    public enum Stage2EnemyMoveType
    {
        Crow,  // 揺れながら中心へ接近、速度：中
        Rat,   // 直線的に中心へ接近、速度：高
        Drone, // 一定間隔で揺れつつ直進で接近（暫定仕様、要調整）、速度：中
    }

    [RequireComponent(typeof(Collider2D))]
    public class Stage2EnemyCharacter : MonoBehaviour, IHittable
    {
        [Header("種別・速度")]
        public Stage2EnemyMoveType moveType;
        public float crowSpeed = 3f;
        public float ratSpeed = 5f;
        public float droneSpeed = 2.5f;

        [Tooltip("元画像が無反転(flipX=false)で右向きに描かれている場合はON")]
        public bool baseFacesRight = false;

        [Header("接近時の揺れ（ネズミ以外に適用）")]
        public float wobbleAmplitude = 0.6f;
        public float wobbleFrequency = 2f;

        [Range(0f, 1f)]
        [Tooltip("ネズミ専用。移動方向のy成分をこの倍率まで弱め、x軸中心の動きにする（0=水平のみ、1=通常通り）")]
        public float ratVerticalMovementFactor = 0.2f;

        [Header("お宝を奪う距離")]
        public float stealRadius = 0.3f;

        [Header("盗んだ後、画面内を徘徊してから逃げる")]
        public Vector2 boundsMin = new Vector2(-8f, -4f);
        public Vector2 boundsMax = new Vector2(8f, 4f);
        public Vector2 wanderDurationRange = new Vector2(10f, 15f);
        public float wanderSpeed = 2.5f;

        [Header("逃走（徘徊時間が終わった後に画面外へ）")]
        public float fleeSpeed = 4f;
        public float despawnDistance = 12f; // お宝からこの距離離れたら逃げ切り、持ち去った点は失われる

        [Header("盗む点数のテーブル（重み。stealWeights の比率でランダム抽選）")]
        public int[] stealAmounts = { 200, 120, 60, 30 };
        public float[] stealWeights = { 0.5f, 1.5f, 5f, 3f };

        [Header("ドル袋（stealAmounts と同じ並び。大きい額ほど大きい袋を割り当てる）")]
        [Tooltip("キャラクターの子オブジェクトとして、少し下の位置に配置したSpriteRendererを指定する。盗むまでは非表示")]
        public SpriteRenderer dollBagRenderer;
        public Sprite[] dollBagSprites = new Sprite[4];
        [Tooltip("ドル袋の表示サイズ（dollBagRenderer の localScale に反映される）")]
        public Vector3 dollBagScale = Vector3.one;

        [Header("ヒット時のフェード消滅・パーティクル・効果音")]
        public float fadeOutDuration = 0.4f;
        public GameObject[] hitEffectPrefabs;
        public AudioClip[] hitSounds;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        [Header("お宝を盗んだ瞬間の効果音")]
        public AudioClip[] stealSounds;
        [Range(0f, 1f)] public float stealSoundVolume = 1f;

        // 破棄されたときに呼ばれる。Stage2EnemySpawner が出現数管理に使う
        public event System.Action OnRemoved;

        enum EnemyState { Approaching, Wandering, Fleeing }

        SpriteRenderer sr;
        Collider2D col;
        bool defeated;
        EnemyState state = EnemyState.Approaching;
        bool hasLoot;
        int lootAmount;
        PlayerSide targetSide;
        Vector3 targetCenter;
        float wobbleTime;
        float wanderTimer;
        Vector3 wanderTarget;

        void Awake()
        {
            sr = GetComponentInChildren<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            SetFacingRight(false); // 初期状態は左向き

            if (dollBagRenderer != null)
            {
                dollBagRenderer.gameObject.SetActive(false);
                dollBagRenderer.transform.localScale = dollBagScale;
            }
        }

        void Start()
        {
            targetSide = Stage2TreasureManager.Instance.GetSideFromWorldX(transform.position.x);
            targetCenter = Stage2TreasureManager.Instance.GetTreasureWorldPosition(targetSide);
        }

        void Update()
        {
            if (defeated) return;

            switch (state)
            {
                case EnemyState.Approaching:
                    MoveApproach();
                    break;
                case EnemyState.Wandering:
                    MoveWander();
                    break;
                case EnemyState.Fleeing:
                    MoveFlee();
                    break;
            }
        }

        void MoveApproach()
        {
            Vector3 toTarget = targetCenter - transform.position;
            float dist = toTarget.magnitude;

            if (dist <= stealRadius)
            {
                Steal();
                return;
            }

            Vector3 dir = ApplyRatBias(toTarget / dist);
            Vector3 move = dir * GetApproachSpeed();

            if (moveType != Stage2EnemyMoveType.Rat)
            {
                wobbleTime += Time.deltaTime;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0);
                move += perp * Mathf.Sin(wobbleTime * wobbleFrequency) * wobbleAmplitude;
            }

            transform.position += move * Time.deltaTime;
            FaceDirection(move.x);
        }

        // ネズミはy軸方向の移動量を弱め、x軸中心の動きにする
        Vector3 ApplyRatBias(Vector3 dir)
        {
            if (moveType != Stage2EnemyMoveType.Rat) return dir;

            dir.y *= ratVerticalMovementFactor;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : dir;
        }

        float GetApproachSpeed()
        {
            switch (moveType)
            {
                case Stage2EnemyMoveType.Crow: return crowSpeed;
                case Stage2EnemyMoveType.Rat: return ratSpeed;
                case Stage2EnemyMoveType.Drone: return droneSpeed;
            }
            return crowSpeed;
        }

        void Steal()
        {
            state = EnemyState.Wandering;
            wanderTimer = Random.Range(wanderDurationRange.x, wanderDurationRange.y);
            PickNewWanderTarget();

            int candidate = PickStealAmount();
            int stolen = Stage2TreasureManager.Instance.StealPoints(targetSide, candidate);
            lootAmount = stolen;
            hasLoot = stolen > 0;

            // キャラクター自体の見た目は変えず、少し下にドル袋を追加表示する
            if (hasLoot && dollBagRenderer != null)
            {
                int tier = GetDollBagTierIndex(candidate);
                if (dollBagSprites != null && tier >= 0 && tier < dollBagSprites.Length && dollBagSprites[tier] != null)
                {
                    dollBagRenderer.sprite = dollBagSprites[tier];
                }
                dollBagRenderer.gameObject.SetActive(true);
            }

            if (hasLoot) PlayStealSound();
        }

        void PlayStealSound()
        {
            if (stealSounds == null || stealSounds.Length == 0) return;
            var clip = stealSounds[Random.Range(0, stealSounds.Length)];
            SfxPlayer.Play(clip, stealSoundVolume);
        }

        int PickStealAmount()
        {
            float total = 0f;
            for (int i = 0; i < stealWeights.Length; i++) total += stealWeights[i];

            float r = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < stealWeights.Length; i++)
            {
                acc += stealWeights[i];
                if (r <= acc) return stealAmounts[i];
            }
            return stealAmounts[stealAmounts.Length - 1];
        }

        int GetDollBagTierIndex(int amount)
        {
            for (int i = 0; i < stealAmounts.Length; i++)
            {
                if (stealAmounts[i] == amount) return i;
            }
            return 0;
        }

        // 盗んだ直後にすぐ逃げ切ってしまわないよう、しばらく画面内をうろつかせる
        void MoveWander()
        {
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                state = EnemyState.Fleeing;
                return;
            }

            Vector3 toTarget = wanderTarget - transform.position;
            float dist = toTarget.magnitude;
            if (dist < 0.2f)
            {
                PickNewWanderTarget();
                return;
            }

            Vector3 dir = ApplyRatBias(toTarget / dist);
            transform.position += dir * wanderSpeed * Time.deltaTime;
            FaceDirection(dir.x);
        }

        void PickNewWanderTarget()
        {
            wanderTarget = new Vector3(
                Random.Range(boundsMin.x, boundsMax.x),
                Random.Range(boundsMin.y, boundsMax.y),
                0);
        }

        void MoveFlee()
        {
            Vector3 dir = (transform.position - targetCenter);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.left;
            dir = ApplyRatBias(dir);

            transform.position += dir * fleeSpeed * Time.deltaTime;
            FaceDirection(dir.x);

            if (Vector3.Distance(transform.position, targetCenter) >= despawnDistance)
            {
                Destroy(gameObject); // 逃げ切り。持ち去った点数はそのまま失われる
            }
        }

        void FaceDirection(float dx)
        {
            if (Mathf.Abs(dx) < 0.0001f) return;
            SetFacingRight(dx > 0f);
        }

        void SetFacingRight(bool facingRight)
        {
            sr.flipX = baseFacesRight ? !facingRight : facingRight;
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

            // ドル袋を持っている状態で倒すと、倒した側のプレイヤーに盗まれた点数が入る
            // (盗まれた本人が倒せば取り返し、相手側が倒せば横取りになる)
            if (hasLoot)
            {
                PlayerSide hitterSide = Stage2TreasureManager.Instance.GetSideFromWorldX(worldPos.x);
                Stage2TreasureManager.Instance.AddPoints(hitterSide, lootAmount);
            }

            SpawnHitEffect();
            PlayHitSound();

            transform.DOKill();
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOPunchScale(Vector3.one * 0.3f, 0.2f, 1, 0));
            seq.Append(sr.DOFade(0f, fadeOutDuration));
            if (hasLoot && dollBagRenderer != null)
            {
                seq.Join(dollBagRenderer.DOFade(0f, fadeOutDuration));
            }
            seq.OnComplete(() => Destroy(gameObject));
        }
    }
}
