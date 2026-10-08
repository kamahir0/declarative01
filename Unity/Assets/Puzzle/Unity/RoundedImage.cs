using UnityEngine;

namespace DeclarativePuzzle.Unity
{
    public sealed class RoundedImage : UnityEngine.UI.Image
    {
        [SerializeField] float radius = 12;
        public float Radius { get => radius; set { radius = value; SetVerticesDirty(); } }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear();
            var rect = GetPixelAdjustedRect();
            var r = Mathf.Min(radius, rect.width * .5f, rect.height * .5f);
            helper.AddVert(rect.center, color, Vector2.zero);
            const int segments = 8;
            var count = 0;
            for (var corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - r : rect.xMin + r,
                    corner < 2 ? rect.yMax - r : rect.yMin + r);
                for (var step = 0; step <= segments; step++)
                {
                    var angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                    helper.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, color, Vector2.zero);
                    count++;
                }
            }
            for (var i = 1; i <= count; i++) helper.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}
