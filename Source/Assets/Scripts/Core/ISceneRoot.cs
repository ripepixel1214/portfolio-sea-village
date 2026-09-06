namespace SeaVillage.Core
{
    /// <summary>
    /// 한 씬의 도메인 오브젝트를 모아 초기화·소유하는 씬 총괄 매니저 계약.
    /// 씬 루트는 자신의 Awake에서 GameManager.RegisterSceneRoot로 등록만 하고,
    /// 실제 시동은 SceneChanger가 화면 전환(Fade In)이 끝난 뒤 호출하는 Initialize()에서 수행한다.
    /// </summary>
    public interface ISceneRoot
    {
        /// <summary>
        /// 씬 전환 애니메이션이 끝나 씬이 완전히 준비된 뒤 호출되는 시동 진입점.
        /// </summary>
        void Initialize();
    }
}
