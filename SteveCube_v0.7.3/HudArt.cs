using UnityEngine;

namespace SteveCube
{
    // Arte da HUD (pixel art 80x32 em PNG/base64, embutida na DLL): coracao, armadura, fome,
    // perola, tabuas, TNT, picareta e um cubo-molde (branco) que e tingido para cada ingrediente.
    internal static class HudArt
    {
        const int AW = 80, AH = 48;
        static Texture2D _atlas;

        // retangulos de UV (origem embaixo-esquerda, como o Unity espera)
        public static Rect HeartFull, HeartEmpty, ArmorFull, ArmorEmpty, FoodFull, FoodEmpty;
        public static Rect Pearl, Planks, Tnt, Pickaxe, CubeMold, Obsidian, Rocket;

        const string Png =
            "iVBORw0KGgoAAAANSUhEUgAAAFAAAAAwCAYAAACG5f33AAAH/klEQVR4nO2aXWxT1x3Af/c6CYSFBsFlu2mphwarEFIzFK0dqzxW" +
            "VRNlUltlrKrYhnhAPPRpDxUPaA+M5QEhNWNV98A0RTwgHlYJsWhbWJdWFUMWczJmRdkWWZnbbekHXmun+Whtx8Q+e7g+1/fj3Gs7" +
            "H5Vw85Os3I//+eDn/zn33IM1LAQ1NMU5TcStpmxYeQBi0S32/fhM3ne/0XrWCg0QQlTbMgy0XA6xY0ctIJez444fPw5AOp0mkUhw" +
            "8OBBOy6RSNhxmUwGANM0Q68dOnQIgFu3boVds4lFt4gLR/fb52euTwVJFLFYzD6Jx+O+utYK3T4yDKtlhzwV6XQawCUviKo0W14Q" +
            "VWm2PBVS3r4nnrCvXTi635WRVUQsFmP79u21spZMb9yaYAmsyquHlNcMTnk3b8YD45zyFhYWAuNSt2831O7s7GxDcatFD7v5mjV8" +
            "6zI3N7cWfWmaxPSHDcWFfSGrRQc0TSHqtVyOY9ahnDu06jznYm5ujlQq5YqrznMubt6Mc+zY86646jznYmFhgYmJCWecPUzPXJ+y" +
            "4xLTHzL8j6x3DhRgz3l2fZOTk6761pK26l9Ny+WEnP8U8iRaIpEQcv5TyFtzYtEtYjg5A0B/X9Ql0Svv8OHDAIyOjrokrmf/nEPY" +
            "zsQAeXaczMT1lqciPpPX5KdOqOb4rBvKJUCDjYbFhT3xgtZ8gXFyCDcgTdb3mX6pG6yCdfmmTNMUABd//E2iZjfP/+R1ADKZTEPt" +
            "yfLDw8MA9Pf3N1V+tcj27969C0BPT09g++oORXeph9bMe6H/ANnwtfNHmMnMEzW7a0Uz87z06l8CO+IsL8V5aVakYVj1ZbPNfXFS" +
            "nBeVSH/F0V1i55FvQXsbWkQHAWK5jCiVyL5xWynRKQ5QynOeezOynjgv9URKccmJSQD6DvQCwSLrifPiFOmuMLpL7HzuKfTOTWgd" +
            "7RCJgBCIe8uIpRIiX+SjP95ySTRNU0hxUF+e81yKDBJ3+vRpAAYHB5X3+/v7fRINwxRSXDI5AUBf3wHr74Fen0TTNEWj4rz09PTY" +
            "60Bb3u9/+XMAjr48iNYWsTKwdI9KoYjQdYynY2T/FBdS4vmTvXYVzcgDOBb7IifSeUor6j6c69B50XEu5Ulxp06eAGDo8hXAysi+" +
            "A73CKXFkZGSFrcPZs2erAqvDVu/cBMD3f/EK+gNdaO3tgEAslaAtQkUI9HvL7HjycXI3sSVOpz8A4JG9D9qVh8m7diPp6kjHi9Yu" +
            "T+lXV13XgzJv4oWjvmtOeVKcFHXq5AkBlkiVxGTS6k9fX1+wLQeXLl2yj2sZ2N6G1tHO0ZcH0R/oQt/6BfTNm0AIKvkiVARi6R5a" +
            "xxJae63Y39/O+o4f+9qXlfIGL/9Z0Z2IfRQkUqISB3553mGazWY0wzBF+vpLXBucVUqE+iKd4iS2CS2iQySC1hZBa29H37wJ/YEu" +
            "aw4UAq1YtIZ0RAe99gLz6B7/Tk7xk0+ZTn/KI3sfZCYzz3jybcaB3eZWf6/Sed8lr8ggcU4uXnyVq1eGlA8KwzDFmWcMJt6Z5fWp" +
            "CueB4ydOcfXKkHK14RWpEieppZIAhKiu5UX1uPaZH5+ia89Djpj6TKc/YHI6fC8w+qZ/g8LJnTt32P3ry8EBR46QzWa0q1eGfBkF" +
            "fnnNkEwmGRsbC42xU0ksl62nbekeYqlEJV+kPL9IeX6Rj0cTbO3da91fLkO53FRHPgsakSdjwrK1WewMFKWSJa5QhLYIVKxhOz8+" +
            "xdbevSxOptmysxtRXLIeKlWcc6CK/2QWV9vHFREkLyxbV4IlcOY9LfvGbbHzu4cQuk5FVB8YbRG69jxEZW6RLTu7qXySR+SL5OJ/" +
            "s9eCqjnQiSgvr0U/myJInmSt5IH3TSS6SxhPx9A7N1cX0rr1EFkuW5mXL5J9K+FbSDvXgl7C5sDfxK0d5RtmV2DM+Kz/ISM5V6oo" +
            "F9Jh8ryYpinC1oJhc+DAwID6VW7Hk49bSxXdepWjXEYslVyZ5+0EuBfVEpVAKc77KqcSqRJ4rlRxlZc0K8/bf5VIlcCBgQG7/XXZ" +
            "TPjBt2sL6n+//7F9nEgX7IbDyjtFOgUGiYOVy1O17xTpFOgUJ6+t63bW+ZO9TE5nfBnXaPkbZhfjs/lQcU6a3X2p1/7IyAhjY2NK" +
            "cZLQTDDaqrsYy5OBFay4k4YhMtnsfb9z7NuZgJo4L2sl0jQMcTd9hp69F7jfJbom8SBxXlYq0jQMa9+tReSB9X+4olFxXrLLk43P" +
            "a4Yh7v6hDwyDnoOjLSEPQKewTXnjqf2n6hYuzEfqxriYmYHh4J933I9oZvc+e8libI0qg/aZh0llRu3zd3P/tI/nC+/XzSTTMEQq" +
            "lSSVStHf/8OWyT5w7sYA2UXrFwBekVKeU1yjOOW1IsofF339Ky/YMsESJ+V9Y8+PGq7cK6/Vsg88Q1hF16btfLT438D7QUPYNAzx" +
            "1m6ThVcuAq0pDzwZuK3T/6sqp7zvPfazhiqV8sZnF0idONWy8sAjcK6Qoffh5wKDf/vXn9atUA7b8VnrN3lnFvItKw8Uc+Dku7+z" +
            "j2Nf9S9luju/FFiZc87bd2Wo5eUB6HTOBd6M/2vIPn704WcBmC/8z77W2V3b2jcNQ5x+xmzpB4YK16ucalFdKPm35KU4ez+vKm/i" +
            "nVnenLK2+z8P8iBgM8Ep0inQKw4sed/Z3/G5EycJ3c6isI1CaVEpzhXfIltTG2ywwQYbbLDB/cT/AVedqWxMadwoAAAAAElFTkSu" +
            "QmCC";

        static Rect UV(int x, int y, int w, int h)
        {
            return new Rect(x / (float)AW, 1f - (y + h) / (float)AH, w / (float)AW, h / (float)AH);
        }

        public static bool Ensure()
        {
            if (_atlas != null) return true;
            try
            {
                byte[] bytes = System.Convert.FromBase64String(Png);
                Texture2D t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!t.LoadImage(bytes)) return false;
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
                _atlas = t;
            }
            catch { return false; }

            HeartFull = UV(0, 0, 9, 9);   HeartEmpty = UV(10, 0, 9, 9);
            ArmorFull = UV(20, 0, 9, 9);  ArmorEmpty = UV(30, 0, 9, 9);
            FoodFull = UV(40, 0, 9, 9);   FoodEmpty = UV(50, 0, 9, 9);
            Pearl = UV(0, 10, 16, 16);    Planks = UV(16, 10, 16, 16);
            Tnt = UV(32, 10, 16, 16);     Pickaxe = UV(48, 10, 16, 16);
            CubeMold = UV(64, 10, 16, 16);
            Obsidian = UV(0, 26, 16, 16); Rocket = UV(16, 26, 16, 16);
            return true;
        }

        public static void Draw(Rect uv, Rect pos, Color tint)
        {
            if (_atlas == null) return;
            Color old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(pos, _atlas, uv, true);
            GUI.color = old;
        }

        static Color RGB(int r, int g, int b) { return new Color(r / 255f, g / 255f, b / 255f, 1f); }

        // cor do cubo de cada ingrediente (pelo nome em ingles que o jogo usa)
        public static Color IngredientColor(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("potato")) return RGB(214, 170, 96);
            if (n.Contains("lettuce")) return RGB(130, 205, 70);
            if (n.Contains("cucumber")) return RGB(60, 150, 60);
            if (n.Contains("tomato")) return RGB(230, 60, 45);
            if (n.Contains("onion")) return RGB(214, 160, 86);
            if (n.Contains("mushroom")) return RGB(190, 160, 125);
            if (n.Contains("carrot")) return RGB(240, 140, 30);
            if (n.Contains("steak") || n.Contains("beef") || n.Contains("meat")) return RGB(190, 80, 70);
            if (n.Contains("chicken")) return RGB(240, 200, 140);
            if (n.Contains("sausage")) return RGB(180, 70, 60);
            if (n.Contains("fish")) return RGB(120, 170, 210);
            if (n.Contains("prawn") || n.Contains("shrimp")) return RGB(240, 140, 110);
            if (n.Contains("rice")) return RGB(240, 240, 230);
            if (n.Contains("pasta") || n.Contains("noodle")) return RGB(240, 215, 120);
            if (n.Contains("cheese")) return RGB(250, 210, 70);
            if (n.Contains("bread") || n.Contains("bun")) return RGB(222, 176, 110);
            if (n.Contains("dough")) return RGB(235, 220, 180);
            if (n.Contains("egg")) return RGB(245, 240, 225);
            if (n.Contains("chocolate")) return RGB(110, 70, 45);
            if (n.Contains("seaweed")) return RGB(40, 100, 60);
            if (n.Contains("tortilla")) return RGB(235, 210, 150);
            if (n.Contains("plate")) return RGB(235, 235, 240);
            if (n.Contains("extinguisher")) return RGB(210, 40, 40);
            if (n.Contains("pan") || n.Contains("pot")) return RGB(100, 100, 108);
            int h = (n.GetHashCode() & 0x7fffffff) % 360;
            return Color.HSVToRGB(h / 360f, 0.55f, 0.95f);
        }
    }
}
