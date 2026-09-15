namespace Assets.Scripts
{
    public static class ElementState
    {
        public const int Empty = 0, Fire = 100, Water = 200, Electricity = 300, Stone = 400;
        public static int Family(int type) => type / 100;
        public static int BaseType(int type) => type - type % 100;
        public static int Stage(int type) => type % 100;
        public static bool IsReactive(int type) => type >= 100 && Stage(type) < 10;
        public static bool IsFading(int type) => type >= 100 && Stage(type) == 10;
        public static bool IsSpentEffect(int type) => type >= 100 && Stage(type) == 11;
        public static bool IsWall(int type) => Family(type) == 4 && Stage(type) < 10;
        public static bool CanPlaceOn(int type) => type == Empty || IsSpentEffect(type);
    }
}
