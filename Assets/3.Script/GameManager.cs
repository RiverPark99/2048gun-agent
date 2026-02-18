// =====================================================
// GameManager.cs - v6.4
// New Input System: 스와이프 + 키보드 (WASD/Arrow)
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
    [SerializeField] private float swipeThreshold = 50f;

    // 스와이프 추적
    private Vector2 pointerStartPos;
    private bool isPointerDown = false;
    private bool swipeConsumed = false;

    // Input Actions
    private InputAction moveAction;
    private InputAction pointerPressAction;
    private InputAction pointerPositionAction;

    void Awake()
    {
        // 키보드 이동 (WASD + Arrow)
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

        // 포인터(마우스/터치) 프레스
        pointerPressAction = new InputAction("PointerPress", InputActionType.Button, "<Pointer>/press");

        // 포인터 위치
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
        gridManager.Initialize();
        playerHP.Initialize();
        gunSystem.Initialize();
        bossBattle.Initialize();

        gridManager.StartNewGame();
        gunSystem.UpdateGunUI();
        gridManager.UpdateTurnUI();
    }

    // === 키보드 이동 ===
    void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (bossBattle.ShouldBlockInput()) return;
        if (gunSystem.IsGunMode) return;

        Vector2 v = ctx.ReadValue<Vector2>();
        Vector2Int dir = Vector2Int.zero;

        // 가장 큰 축 기준 방향 판별
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            dir = v.x > 0 ? Vector2Int.right : Vector2Int.left;
        else if (Mathf.Abs(v.y) > 0)
            dir = v.y > 0 ? Vector2Int.down : Vector2Int.up;   // Grid: Y 반전

        if (dir != Vector2Int.zero)
            gridManager.Move(dir);
    }

    // === 포인터(터치/마우스) 스와이프 ===
    void OnPointerDown(InputAction.CallbackContext ctx)
    {
        pointerStartPos = pointerPositionAction.ReadValue<Vector2>();
        isPointerDown = true;
        swipeConsumed = false;
    }

    void OnPointerUp(InputAction.CallbackContext ctx)
    {
        if (!isPointerDown || swipeConsumed) { isPointerDown = false; return; }
        isPointerDown = false;

        if (bossBattle.ShouldBlockInput()) return;

        Vector2 endPos = pointerPositionAction.ReadValue<Vector2>();
        Vector2 delta = endPos - pointerStartPos;

        // Gun 모드: 탭(스와이프 아님) → ShootTile
        if (gunSystem.IsGunMode)
        {
            if (delta.magnitude < swipeThreshold)
                gunSystem.ShootTile();
            return;
        }

        // 스와이프 판별
        if (delta.magnitude < swipeThreshold) return;

        Vector2Int dir;
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            dir = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
        else
            dir = delta.y > 0 ? Vector2Int.down : Vector2Int.up;   // Grid: Y 반전

        gridManager.Move(dir);
        swipeConsumed = true;
    }

    // === 다른 시스템에서 호출하는 중계 메서드 ===
    public void SetBossAttacking(bool attacking)
    {
        bossBattle.SetBossAttacking(attacking);
    }

    public bool IsBossAttacking()
    {
        return bossBattle.IsBossAttacking;
    }

    public void SetBossTransitioning(bool transitioning)
    {
        bossBattle.SetBossTransitioning(transitioning);
    }

    public void TakeBossAttack(int damage)
    {
        bossBattle.TakeBossAttack(damage);
    }

    public void OnBossDefeated()
    {
        bossBattle.OnBossDefeated();
    }

    public void UpdateTurnUI()
    {
        gridManager.UpdateTurnUI();
    }
}
