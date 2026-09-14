namespace Odyssey.Domain.Combat
{
    /// <summary>ODY-S05-604: ADR-029 section 6 stage 12/13's durable attack outcome, mirroring stage 3.3's Pending/Accepted vocabulary.</summary>
    public enum AttackOutcomeKind
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3,
        Cancelled = 4,
    }

    /// <summary>ODY-S05-604: the recorded choice a MainGM or Rules-eligible controller makes when resolving a Pending intervention (ADR-029 section 1 rule 6, section 10). Ruleset-specific intervention option payloads remain out of scope (ADR-029 section 1 rule 3 / section 14 item 3); this is only the terminal disposition.</summary>
    public enum AttackInterventionResolution
    {
        Approve = 1,
        Reject = 2,
        Cancel = 3,
    }
}
