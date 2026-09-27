namespace OnePiece.Ev;

public static class ReleaseCalendar
{
    public static SetProfile[] Select(IEnumerable<SetProfile> profiles, string[] enabledSets, DateOnly asOf) => profiles
        .Where(p => p.Enabled && p.ReleaseDate <= asOf && (enabledSets.Length == 0 || enabledSets.Contains(p.Code)))
        .OrderBy(p => p.ReleaseDate).ThenBy(p => p.Code, StringComparer.Ordinal).ToArray();
}
