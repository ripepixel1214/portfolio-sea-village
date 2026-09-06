using UnityEngine;
using System.Collections;
using SeaVillage.Player;
using SeaVillage.UI;
using SeaVillage.Core;

public class BaseInteractable : MonoBehaviour, IInteractable
{
    private SpriteOutliner _spriteOutliner;
    private PlayerInteractor _playerInteractor;
    private Coroutine _bindRoutine;

    [Header("Interactable Settings")]
    [SerializeField] protected InteractionType _interactionType;
    [SerializeField] private string _displayName;

    protected string DisplayName => _displayName;

    /// <summary>표시 이름이 비어 있으면 GameObject 이름으로 폴백.</summary>
    protected string EffectiveDisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;

    // IInteractable Properties
    public bool CanInteract => true;
    public GameObject GameObject => gameObject;
    public SpriteOutliner SpriteOutliner => _spriteOutliner;

    /// <summary>상호작용 타입. 전용 자식 클래스는 고정 타입을 override 로 선언한다(직렬화 값은 베이스 전용).</summary>
    public virtual InteractionType InteractionType => _interactionType;

    protected void Awake()
    {
        if (CanInteract && _spriteOutliner == null)
            _spriteOutliner = GetComponent<SpriteOutliner>();
    }

    protected void Start()
    {
        InitializeInteractable();
    }

    /// <summary>파생 클래스의 1회성 초기화 훅. 플레이어 상호작용 구독은 OnEnable 이 담당한다.</summary>
    protected virtual void InitializeInteractable() { }

    /// <summary>
    /// 매 활성화마다 플레이어 상호작용에 다시 바인딩한다. 껐다 켜지는 오브젝트(건설된 가게 등)도
    /// 재활성 시 정상적으로 재구독하도록 Start 가 아니라 OnEnable 에 묶는다.
    /// </summary>
    protected virtual void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindPlayerInteractor());
    }

    protected virtual void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        UnsubscribeFromEvents();

        // 비활성화 시 하이라이트도 끈다(다음 활성화에서 다시 판정).
        if (_spriteOutliner != null)
            _spriteOutliner.SetHighlight(false);
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private IEnumerator BindPlayerInteractor()
    {
        yield return null;
        yield return new WaitUntil(() => GameManager.HasInstance && GameManager.Instance.HasPlayer);

        _playerInteractor = GameManager.Instance.Player.Interactor;
        if (_playerInteractor == null)
        {
            Debug.LogWarning($"{gameObject.name}: PlayerInteractor를 찾을 수 없습니다.");
            _bindRoutine = null;
            yield break;
        }

        _playerInteractor.OnInteractableEnter += OnPlayerEnterRange;
        _playerInteractor.OnInteractableExit += OnPlayerExitRange;

        // 이미 플레이어 범위에 겹친 채로 활성화된 경우(예: 건설 직후 가게 오브젝트) 트리거 enter 가
        // 발생하지 않으므로 스스로 등록하고, 지나갔을 수 있는 enter 를 대신해 하이라이트를 동기화한다.
        _playerInteractor.RegisterIfOverlapping(this);
        if (_spriteOutliner != null)
            _spriteOutliner.SetHighlight(ReferenceEquals(_playerInteractor.GetCurrentTarget(), this));

        _bindRoutine = null;
    }

    private void UnsubscribeFromEvents()
    {
        if (_playerInteractor != null)
        {
            _playerInteractor.OnInteractableEnter -= OnPlayerEnterRange;
            _playerInteractor.OnInteractableExit -= OnPlayerExitRange;
        }
    }

    public virtual void Interact()
    {
        switch (InteractionType)
        {
            case InteractionType.Ship:
                if (UIManager.Instance.OpenPanel<ShipPanel>() != null)
                    TutorialEventReporter.Report(TutorialEventType.InteractionCompleted, TutorialTargetIds.Ship, source: TutorialEventSource.World);
                break;
            case InteractionType.BulletinBoard:
                UIManager.Instance.OpenPanel<BulletinBoardPanel>();
                break;
            case InteractionType.Shop:
            case InteractionType.SpecialShop:
            case InteractionType.PlayerShopLot:
            case InteractionType.PlayerShop:
                Debug.LogWarning($"{gameObject.name}: {_interactionType} 타입은 전용 Interactable 또는 통합 상호작용 컴포넌트를 사용해야 합니다.");
                break;
        }
    }

    private void OnPlayerEnterRange(IInteractable interactable)
    {
        if (interactable as Object == this)
        {
            if (_spriteOutliner != null)
                _spriteOutliner.SetHighlight(true);
        }
    }

    private void OnPlayerExitRange(IInteractable interactable)
    {
        if (interactable as Object == this)
        {
            if (_spriteOutliner != null)
                _spriteOutliner.SetHighlight(false);
        }
    }
}
