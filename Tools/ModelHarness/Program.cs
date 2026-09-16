using System;
using Game.Core;

/// <summary>
/// Standalone host for the pure-C# Game.Core models. Runs with no Unity Editor and no scene,
/// proving the authoritative gameplay state is engine-independent.
///
/// Build/run: Tools/ModelHarness/build-and-run.ps1
/// </summary>
internal static class Program
{
    private static int failures;

    private static int Main()
    {
        Check("damage clamps and reports applied", () =>
        {
            var s = new NpcState("sword_guard", 30, 30, 0, 0);
            if (s.ApplyDamage(12) != 12) throw new Exception("applied != 12");
            if (s.Hp != 18) throw new Exception("hp != 18");
        });

        Check("damage never goes below zero and kills", () =>
        {
            var s = new NpcState("sword_guard", 10, 10, 0, 0);
            if (s.ApplyDamage(999) != 10) throw new Exception("applied != 10");
            if (s.Hp != 0) throw new Exception("hp != 0");
            if (s.IsAlive) throw new Exception("should be dead");
        });

        Check("heal clamps to max and does not revive dead", () =>
        {
            var s = new NpcState("sword_guard", 20, 5, 0, 0);
            if (s.Heal(10) != 10) throw new Exception("heal != 10");
            s.SetHp(0);
            if (s.Heal(5) != 0 || s.Hp != 0) throw new Exception("revived while dead");
        });

        Check("move cell updates logical position", () =>
        {
            var s = new NpcState("sword_guard", 30, 30, -1, -4);
            s.MoveToCell(3, 7);
            if (s.CellX != 3 || s.CellY != 7) throw new Exception("cell not updated");
        });

        Check("service commands change the model", () =>
        {
            var session = new GameSession();
            session.NpcStates.Register("sword_guard", 30, 30, 0, 0);
            session.NpcStates.ApplyDamage("sword_guard", 5);
            session.NpcStates.MoveToCell("sword_guard", 4, -2);
            if (!session.NpcStates.TryGet("sword_guard", out NpcState s)) throw new Exception("missing");
            if (s.Hp != 25 || s.CellX != 4 || s.CellY != -2) throw new Exception("state wrong");
        });

        Check("snapshot round trip restores state after a fresh launch", () =>
        {
            var session = new GameSession();
            session.NpcStates.Register("sword_guard", 30, 30, 0, 0);
            session.NpcStates.ApplyDamage("sword_guard", 8);
            session.NpcStates.MoveToCell("sword_guard", 6, 6);
            if (!session.NpcStates.TryCapture("sword_guard", out NpcStateSnapshot snap)) throw new Exception("capture failed");

            var reloaded = new GameSession();
            reloaded.NpcStates.Apply(snap);
            reloaded.NpcStates.TryGet("sword_guard", out NpcState restored);
            if (restored.Hp != snap.Hp || restored.MaxHp != snap.MaxHp ||
                restored.CellX != snap.CellX || restored.CellY != snap.CellY)
                throw new Exception("restore mismatch");
        });

        Check("destroying and recreating a view keeps the model", () =>
        {
            var session = new GameSession();
            session.NpcStates.Register("sword_guard", 30, 30, 0, 0);
            NpcState viewA = session.NpcStates.Register("sword_guard", 30, 30, 0, 0);
            session.NpcStates.ApplyDamage("sword_guard", 9);
            viewA = null; // "destroy" the view
            NpcState viewB = session.NpcStates.Register("sword_guard", 30, 30, 0, 0);
            if (viewB.Hp != 21) throw new Exception("model reset by view recycle");
        });

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(string name, Action body)
    {
        try
        {
            body();
            Console.WriteLine("  PASS  " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("  FAIL  " + name + " -> " + ex.Message);
        }
    }
}
