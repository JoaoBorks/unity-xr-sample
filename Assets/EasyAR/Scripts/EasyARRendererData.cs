using UnityEngine;

[CreateAssetMenu(fileName = "EasyARRendererData", menuName = "EasyAR/Data")]
public class EasyARRendererData : ScriptableObject
{
    public RenderTexture RenderTexture { get; private set; }

    [HideInInspector]
    public Material material;

    public bool UpdateTexture(Camera cam, Material material, out RenderTexture tex)
    {
        tex = RenderTexture;
        if (!cam || !material)
        {
            if (RenderTexture)
            {
                Destroy(RenderTexture);
                tex = RenderTexture = null;
                return true;
            }
            return false;
        }
        int w = (int)(Screen.width * cam.rect.width);
        int h = (int)(Screen.height * cam.rect.height);
        if (RenderTexture && (RenderTexture.width != w || RenderTexture.height != h))
            Destroy(RenderTexture);

        if (RenderTexture)
            return false;
        else
        {
            RenderTexture = new RenderTexture(w, h, 0);
            tex = RenderTexture;
            return true;
        }
    }
}
