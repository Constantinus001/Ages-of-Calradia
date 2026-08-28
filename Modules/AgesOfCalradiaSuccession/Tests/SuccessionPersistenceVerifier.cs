using System;
using System.Collections.Generic;
using AgesOfCalradiaSuccession;

internal static class SuccessionPersistenceVerifier
{
    private static int Main()
    {
        Dictionary<string, string> laws = new Dictionary<string, string> { { "empire|west", "AbsolutePrimogeniture" } };
        Dictionary<string, string> dynasties = new Dictionary<string, string> { { "empire|west", "clan%one" } };
        Dictionary<string, string> monarchs = new Dictionary<string, string> { { "empire|west", "hero\nvaleron" } };
        Dictionary<string, string> minors = new Dictionary<string, string> { { "empire|west", "minor-heir" } };
        Dictionary<string, string> regents = new Dictionary<string, string> { { "empire|west", "adult-regent" } };
        string payload = SuccessionPersistence.Serialize(laws, dynasties, monarchs, minors, regents);

        Dictionary<string, string> loadedLaws = new Dictionary<string, string>();
        Dictionary<string, string> loadedDynasties = new Dictionary<string, string>();
        Dictionary<string, string> loadedMonarchs = new Dictionary<string, string>();
        Dictionary<string, string> loadedMinors = new Dictionary<string, string>();
        Dictionary<string, string> loadedRegents = new Dictionary<string, string>();
        SuccessionPersistence.Deserialize(payload, loadedLaws, loadedDynasties, loadedMonarchs, loadedMinors, loadedRegents);

        if (loadedLaws["empire|west"] != "AbsolutePrimogeniture") return Fail("law round-trip");
        if (loadedDynasties["empire|west"] != "clan%one") return Fail("dynasty round-trip");
        if (loadedMonarchs["empire|west"] != "hero\nvaleron") return Fail("monarch round-trip");
        if (loadedMinors["empire|west"] != "minor-heir") return Fail("minor heir round-trip");
        if (loadedRegents["empire|west"] != "adult-regent") return Fail("regent round-trip");

        SuccessionPersistence.Deserialize("v1\nignored", loadedLaws, loadedDynasties, loadedMonarchs, loadedMinors, loadedRegents);
        if (loadedLaws.Count != 0 || loadedDynasties.Count != 0 || loadedMonarchs.Count != 0 || loadedMinors.Count != 0 || loadedRegents.Count != 0) return Fail("unknown version rejection");

        SuccessionPersistence.Deserialize("v2\nrealm|HouseSeniority|old_house|old_monarch", loadedLaws, loadedDynasties, loadedMonarchs, loadedMinors, loadedRegents);
        if (loadedLaws["realm"] != "HouseSeniority" || loadedMinors.Count != 0 || loadedRegents.Count != 0) return Fail("v2 migration");

        Dictionary<string, string> legitimacy = new Dictionary<string, string> { { "realm:clan|one", "72.5" } };
        Dictionary<string, string> recognition = new Dictionary<string, string> { { "realm:clan|one", "Recognized" } };
        string politics = SuccessionPoliticsPersistence.Serialize(legitimacy, recognition);
        Dictionary<string, string> loadedLegitimacy = new Dictionary<string, string>();
        Dictionary<string, string> loadedRecognition = new Dictionary<string, string>();
        SuccessionPoliticsPersistence.Deserialize(politics, loadedLegitimacy, loadedRecognition);
        if (loadedLegitimacy["realm:clan|one"] != "72.5" || loadedRecognition["realm:clan|one"] != "Recognized") return Fail("politics round-trip");

        SuccessionPoliticsPersistence.Deserialize("v1\nrealm|%ZZ|Recognized\n%ZZ|40|Neutral", loadedLegitimacy, loadedRecognition);
        if (!loadedLegitimacy.ContainsKey("realm") || loadedLegitimacy["realm"] != string.Empty
            || loadedRecognition["realm"] != "Recognized" || loadedLegitimacy.ContainsKey("%ZZ"))
            return Fail("malformed politics isolation");
        Console.WriteLine("Succession persistence verifier passed.");
        return 0;
    }

    private static int Fail(string name)
    {
        Console.Error.WriteLine("Succession persistence verifier failed: " + name + ".");
        return 1;
    }
}
