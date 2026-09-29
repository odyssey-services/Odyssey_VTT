namespace Odyssey.Domain.Geometry
{
    /// <summary>SLICE-10 Block 2: the three obstacle shapes this task implements -- a full flag set (Locked/Hidden/Destroyed/Broken/Boarded) is a deliberate non-goal, see the task contract section 3.</summary>
    public enum ObstacleKind
    {
        Wall,
        Door,
        Window,
    }

    /// <summary>
    /// SLICE-10 Block 2: pure blocking-rule functions over <see cref="ObstacleKind"/>, kept in
    /// <c>Odyssey.Domain</c> (no dependency on any other module, ADR-001 section 5) next to
    /// <see cref="BoardGeometry"/> so a future Block 3 line-of-sight implementation can call these
    /// directly without a Domain-to-Application dependency. <paramref name="isOpen"/> is meaningful
    /// only for <see cref="ObstacleKind.Door"/> (null for Wall/Window, where it is ignored); a
    /// closed door blocks both, an open door blocks neither. Windows always give vision but always
    /// block movement, independent of <paramref name="isOpen"/> -- windows do not toggle in this task.
    /// </summary>
    public static class ObstacleGeometry
    {
        public static bool BlocksVision(ObstacleKind kind, bool? isOpen)
        {
            return kind switch
            {
                ObstacleKind.Wall => true,
                ObstacleKind.Window => false,
                ObstacleKind.Door => isOpen != true,
                _ => true,
            };
        }

        public static bool BlocksMovement(ObstacleKind kind, bool? isOpen)
        {
            return kind switch
            {
                ObstacleKind.Wall => true,
                ObstacleKind.Window => true,
                ObstacleKind.Door => isOpen != true,
                _ => true,
            };
        }
    }
}
