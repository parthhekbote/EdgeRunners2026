using UnityEngine;

namespace SyntyCombined
{
    /// <summary>
    /// Generates high-tech Sci-Fi tactical map preview textures for Cyber City sectors.
    /// Provides real visual map graphics for Main Cyber Plaza, Cyber Alley, and Overlook Tower.
    /// </summary>
    public static class CyberCityMapAssets
    {
        private static Sprite _plazaMapSprite;
        private static Sprite _alleyMapSprite;
        private static Sprite _towerMapSprite;

        public static Sprite GetSectorMapSprite(int sectorIndex)
        {
            switch (sectorIndex)
            {
                case 0:
                    if (_plazaMapSprite == null) _plazaMapSprite = CreateSectorTexture(new Color(0.05f, 0.25f, 0.45f), new Color(0.2f, 0.85f, 1f), "SECTOR 01: PLAZA CENTRAL");
                    return _plazaMapSprite;

                case 1:
                    if (_alleyMapSprite == null) _alleyMapSprite = CreateSectorTexture(new Color(0.35f, 0.12f, 0.05f), new Color(1f, 0.4f, 0.2f), "SECTOR 02: DOWNTOWN ALLEY");
                    return _alleyMapSprite;

                case 2:
                    if (_towerMapSprite == null) _towerMapSprite = CreateSectorTexture(new Color(0.25f, 0.08f, 0.35f), new Color(0.85f, 0.3f, 1f), "SECTOR 03: OVERLOOK TOWER");
                    return _towerMapSprite;

                default:
                    return GetSectorMapSprite(0);
            }
        }

        private static Sprite CreateSectorTexture(Color bgColor, Color gridColor, string label)
        {
            int width = 512;
            int height = 512;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Base background
                    Color c = bgColor * 0.4f;

                    // Grid pattern
                    if (x % 32 == 0 || y % 32 == 0)
                    {
                        c = gridColor * 0.3f;
                    }

                    // Border box
                    if (x < 8 || x > width - 8 || y < 8 || y > height - 8)
                    {
                        c = gridColor * 0.9f;
                    }

                    // Tactical radar rings
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(256, 256));
                    if (Mathf.Abs(dist - 120f) < 2f || Mathf.Abs(dist - 200f) < 2f)
                    {
                        c = gridColor * 0.7f;
                    }

                    // Center crosshair
                    if (Mathf.Abs(x - 256) < 2 || Mathf.Abs(y - 256) < 2)
                    {
                        if (dist < 220f) c = gridColor;
                    }

                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}
