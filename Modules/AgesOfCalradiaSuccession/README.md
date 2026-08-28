# Ages of Calradia Succession

Standalone ownership boundary for the future hereditary succession system.

## Module responsibility

This module will exclusively own:

- hereditary succession laws and heir ordering;
- dynastic claims, legitimacy, pretenders, and disputed inheritance;
- regencies for underage or incapacitated rulers;
- recognition, coronation, usurpation, and succession crises;
- succession-driven civil wars and claimant settlements.

The design will not select rulers through a kingdom vote. Succession must first
resolve the lawful hereditary heir, then evaluate recognition and opposition to
that heir. Political resistance may create a pretender or civil war, but it
does not turn inheritance into an election.

## Boundary with the religion module

`AgesOfCalradiaReligions` continues to own personal faith, piety, clergy
relations, realm faith, and religious legitimacy. This module reads those
values through `ReligionService`; it never writes religion population cohorts
or clergy state.

## Hereditary core v0.2.0

The module now stores a versioned succession law, ruling dynasty, and last
recognized monarch for every kingdom. When Bannerlord creates its native king
selection decision, the module ranks eligible hereditary claimants, cancels the
vote, and transfers the crown with Bannerlord's supported ruling-clan action.
If the normal claimant list is empty, a deterministic emergency order prefers
the surviving ruling house, adults, higher-tier houses, and then renown. It does
not restore voting. If a kingdom has no living clan leader at all, the empty
vote is cancelled and the incident is logged rather than manufacturing a ruler.

Default laws are culture-based: imperial realms use absolute primogeniture,
Vlandian realms use male-preference primogeniture, Aserai realms use agnatic
primogeniture, Battanian/Sturgian/Nord realms use house seniority, and Khuzait
realms use nomadic house seniority. Religious legitimacy is a secondary
claim-strength input; it cannot defeat a valid continuation of the ruling house.

The public `SuccessionService` exposes the current law and an ordered claimant
ledger for later debug and management UI work without coupling that UI to the
succession engine.

## Debug ruler-death test v0.2.1

Town, castle, and village menus include **[DEBUG] Kill a ruler to test
succession**. It opens a list of living rulers, displays each realm's succession
law, and requires a second confirmation. The action uses Bannerlord's native
old-age death path so it exercises the same ruler-death and succession events as
normal play. The player character is never an eligible target.

## Underage heirs and regencies v0.3.0

Primogeniture now follows the former monarch's child branches before collateral
relatives. If the lawful heir is younger than eighteen, the child is recorded as
heir while a deterministic adult regent governs the kingdom. The regent does not
become the new dynasty. If a regent dies, the same child remains heir and another
adult is appointed without a vote. At adulthood, Bannerlord's supported clan-
leader and ruling-clan actions transfer authority to the heir and close the
regency. Heir and regent identities are stored in the versioned campaign state;
v0.2 saves migrate with empty regency fields.

## Legitimacy, recognition, and claimant wars v0.4.3

Every accession now receives a 0–100 legitimacy score derived from its dynastic
basis, the heir's age, culture, personal faith, religious legitimacy, regency,
and coronation. Each non-mercenary clan deterministically recognizes the ruler,
remains neutral, opposes the accession, or supports the strongest recorded
pretender. These are recognition states, not votes.

Player rulers may hold a coronation from a town or castle menu. AI adult rulers
coronate after seven days. Regents cannot crown themselves in place of a child.
Legitimacy, coronation, accession basis, pretender, and clan recognition persist
in a separate versioned politics payload.

Town, castle, and village menus also provide **[DEBUG] Cause a succession civil
war**. After confirmation it creates a separate claimant kingdom through
Bannerlord's supported kingdom-creation, clan-defection, ruling-clan, and claim-
on-throne war actions. The opening twenty-day peace can immediately suspend the
war, but the claimant split remains. This destructive debug action should only
be used after saving.

This module does not own or modify World Events artwork, the political map,
borders, islands, island exclusions, the calendar, or core UI dimensions.
When a claimant realm forms, Succession gives it the claimant clan's original
banner, applies a lighter or darker variant of the parent kingdom's colours,
and sends a one-shot dirty notification to the existing campaign-map political
border behavior. The protected renderer remains the sole owner of rebuilding
and drawing its territory meshes.
