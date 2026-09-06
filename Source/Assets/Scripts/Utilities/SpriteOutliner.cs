using UnityEngine;

public class SpriteOutliner : MonoBehaviour
{
    private static readonly int UseOutlineId = Shader.PropertyToID("_UseOutline");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineSizeId = Shader.PropertyToID("_OutlineSize");

    [Range(0, 10)]
    public float outlineSize = 1f;

    public Color outlineColor = new Color(1f, 128f / 255f, 0f);

    private MaterialPropertyBlock mpb;
    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        _spriteRenderer.GetPropertyBlock(mpb);

        SetHighlight(false);
    }

    public void SetHighlight(bool enable)
    {
        if (enable)
        {
            mpb.SetFloat(UseOutlineId, 1f);
            mpb.SetColor(OutlineColorId, outlineColor);
            mpb.SetFloat(OutlineSizeId, outlineSize);
        }
        else
        {
            mpb.SetFloat(UseOutlineId, 0f);
        }
        
        _spriteRenderer.SetPropertyBlock(mpb);
    }
}
