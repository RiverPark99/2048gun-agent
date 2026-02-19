// =====================================================
// GameManager.cs - v6.5
// New Input System: 스와이프(임계값 즉시 판정) + 키보드
// =====================================================

using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("System References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GunSystem gunSystem;
    [SerializeField] private PlayerHPSystem playerHP;
    [SerializeField] private BossBattleSystem bossBattle;
    [SerializeField] private BossManager bossManager;

    [Header("Swipe Settings")]
    [SerializeField] private float swipeThresholdBase = 80f; // 1290px 기준

    // 스와이프 추적
    private Vector2 pointerStartPos;
    private bool isPointerDown = false;
    private bool swipeConsumed = false;
    private float swipeThreshold;

    // Input Actions
    private InputAction moveAction;
    private InputAction pointerPressAction;
    private InputAction pointerPositionAction;

    void Awake()
    {
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        pointerPressAction = new InputAction("PointerPress", InputActionType.Button, "<Pointer>/press");
        pointerPositionAction = new InputAction("PointerPosition", InputActionType.Value, "<Pointer>/position");
    }

    void OnEnable()
    {
        moveAction.Enable();
        pointerPressAction.Enable();
        pointerPositionAction.Enable();

        moveAction.performed += OnMovePerformed;
        pointerPressAction.started += OnPointerDown;
        pointerPressAction.canceled += OnPointerUp;
    }

    void OnDisable()
    {
        moveAction.performed -= OnMovePerformed;
        pointerPressAction.started -= OnPointerDown;
        pointerPressAction.canceled -= OnPointerUp;

        moveAction.Disable();
        pointerPressAction.Disable();
        pointerPositionAction.Disable();
    }

    void Start()
    {
        // v6.6: 화면 비율 대응 스와이프 임계값
        swipeThreshold = swipeThresholdBase * (Screen.width / 1290f);

        gridManager.Initialize();
        playerHP.Initialize();
        gunSystem.Initialize();
        bossBattle.Initialize();

        gridManager.StartNewGame();
        gunSystem.UpdateGunUI();
        gridManager.UpdateTurnUI();
    }

    // ⭐ v6.5: 드래그 중 임계값 도달 시 즉시 입력
    void Update()
    {
        if (!isPointerDown || swipeConsumed) return;

        Vector2 currentPos = pointerPositionAction.ReadValue<Vector2>();
        Vector2 delta = currentPos - pointerStartPos;

        if (delta.magnitude >= swipeThreshold)
        {
            swipeConsumed = true;

            if (bossBattle.ShouldBlockInput()) return;
            if (gunSystem.IsGunMode) return;

            Vector2Int dir;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                dir = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
            else
                dir = delta.y > 0 ? Vector2Int.down : Vector2Int.up;

            gridManager.Move(dir);
        }
    }

    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (bossBattle.ShouldBlockInput()) return;
        if (gunSystem.IsGunMode) return;

        Vector2 v = ctx.ReadValue<Vector2>();
        Vector2Int dir = Vector2Int.zero;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            dir = v.x > 0 ? Vector2Int.right : Vector2Int.left;
        else if (Mathf.Abs(v.y) > 0)
            dir = v.y > 0 ? Vector2Int.down : Vector2Int.up;

        if (dir != Vector2Int.zero)
            gridManager.Move(dir);
    }

    void OnPointerDown(InputAction.CallbackContext ctx)
    {
        pointerStartPos = pointerPositionAction.ReadValue<Vector2>();
        isPointerDown = true;
        swipeConsumed = false;
    }

    void OnPointerUp(InputAction.CallbackContext ctx)
    {
        if (!isPointerDown) { isPointerDown = false; return; }

        // Gun 모드: 탭(스와이프 안 됨) → ShootTile
        if (!swipeConsumed && gunSystem.IsGunMode)
        {
            if (!bossBattle.ShouldBlockInput())
                gunSystem.ShootTile();
        }

        isPointerDown = false;
        swipeConsumed = false;
    }

    // === 중계 메서드 ===
    public void SetBossAttacking(bool attacking) { bossBattle.SetBossAttacking(attacking); }
    public bool IsBossAttacking() { return bossBattle.IsBossAttacking; }
    public void SetBossTransitioning(bool transitioning) { bossBattle.SetBossTransitioning(transitioning); }
    public void TakeBossAttack(int damage) { bossBattle.TakeBossAttack(damage); }
    public void OnBossDefeated() { bossBattle.OnBossDefeated(); }
    public void UpdateTurnUI() { gridManager.UpdateTurnUI(); }
}
