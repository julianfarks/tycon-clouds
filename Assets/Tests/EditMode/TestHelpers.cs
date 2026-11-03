using CloudGarden.Buildings;
using CloudGarden.Core;
using CloudGarden.Economy;
using NUnit.Framework;

namespace CloudGarden.Tests
{
    public static class TestHelpers
    {
        /// <summary>New game with lots of coins (resources are still limited by storage).</summary>
        public static GameState Rich()
        {
            GameState s = new GameState();
            s.Res.Amount[0] = 1e12;
            return s;
        }

        public static BuildingInstance Build(GameState s, string id, int x, int y, int region = 0, int rotation = 0)
        {
            BuildingInstance b;
            PlaceResult r = s.TryBuild(id, region, x, y, rotation, out b);
            Assert.AreEqual(PlaceResult.Ok, r, "Build " + id + " at " + x + "," + y);
            return b;
        }

        public static void GrantResearch(GameState s, params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++) s.Research.Add(ids[i]);
            s.ModsDirty = true;
            s.RecomputeMods();
        }

        public static void RunSeconds(GameState s, double seconds, double step = 1.0)
        {
            double t = 0;
            while (t < seconds - 1e-9)
            {
                s.Tick(step);
                t += step;
            }
        }
    }
}
