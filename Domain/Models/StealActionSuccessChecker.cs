namespace Domain.Models
{
    public static class StealActionSuccessChecker
    {
        public static int LatestCollectables { get; set; }
        public static int SecondFromLatestCollectables { get; set; }

        public static bool StolenFrom => LatestCollectables < SecondFromLatestCollectables;
        public static bool StealSucceeded => LatestCollectables > SecondFromLatestCollectables + 1;
    }
}
