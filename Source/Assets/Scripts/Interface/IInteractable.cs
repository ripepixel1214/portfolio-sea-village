using UnityEngine;

/// <summary>
/// 상호작용 가능한 모든 객체를 위한 인터페이스
/// </summary>
public interface IInteractable
{
    public bool CanInteract { get; }
    public GameObject GameObject { get; }
    public SpriteOutliner SpriteOutliner { get; }
    public InteractionType InteractionType { get; } 

    // 상호작용 메서드(e.g. 대화 시작, UI 창 열기, 아이템 획득 등)
    public void Interact();
}