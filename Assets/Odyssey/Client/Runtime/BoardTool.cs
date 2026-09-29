namespace Odyssey.Unity.Client
{
    /// <summary>
    /// SLICE-10 Block 6 part 1: the board's active input tool -- the first such mode-switching concept in
    /// this client (no toolbar/tool-mode mechanic existed before this task). <see cref="Select"/> is the
    /// default and preserves every pre-existing gesture (box-select, token drag, camera pan, the right-click
    /// marker) unchanged; the three drawing tools instead route a left-button board drag to
    /// <see cref="BoardObstacleDrawGesture"/>.
    /// </summary>
    public enum BoardTool
    {
        Select,
        DrawWall,
        DrawDoor,
        DrawWindow,
    }
}
