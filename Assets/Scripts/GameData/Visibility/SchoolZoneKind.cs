namespace Game.Core
{
    /// <summary>
    /// The functional kind of a school zone. Drives which placeholder props the builder places and
    /// lets rules reason about a zone without inspecting Unity objects.
    ///
    /// Unity setup: none (pure C#). Runtime API: none.
    /// </summary>
    public enum SchoolZoneKind
    {
        Hallway,
        Classroom,
        Science,
        Art,
        Auditorium,
        Admin,
        Dining,
        Kitchen,
        Gym,
        Lockers,
        Music,
        Wrestling,
        Workshop,
        Library,
        Study,
    }
}
