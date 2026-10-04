using UnityEngine;

// Looks up a country flag sprite by ISO country code. Flags must live in
// a Resources folder named exactly "Flags", with each file named exactly
// after its ISO code (e.g. Resources/Flags/FR.png, Resources/Flags/US.png).
// Resources.Load caches internally after first load, so repeated lookups
// for the same code are cheap.
public static class CountryFlagDatabase
{
    public static Sprite GetFlag(string countryCode)
    {
        if (string.IsNullOrEmpty(countryCode)) return null;
        return Resources.Load<Sprite>("Flags/" + countryCode.ToUpperInvariant());
    }
}
