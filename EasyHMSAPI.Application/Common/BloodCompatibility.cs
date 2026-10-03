using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// ABO/Rh compatibility between a blood bag and the patient who is about to receive it. This is the
    /// last software gate before a transfusion is recorded, so an unknown or unreadable group never passes.
    /// Bags use the A_POS / O_NEG form; patient registration stores free text such as "A+" or "O -".
    /// </summary>
    public static class BloodCompatibility
    {
        public readonly record struct Group(string Abo, bool RhPositive);

        /// <summary>Parses "A_POS", "A+", "AB -", "o neg" ... Returns null when it is not a definite ABO/Rh group.</summary>
        public static Group? Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = raw.Trim().ToUpperInvariant().Replace(" ", "").Replace("_", "").Replace("-", "NEG").Replace("+", "POS");

            bool rhPositive;
            if (s.EndsWith("POSITIVE")) { rhPositive = true; s = s[..^8]; }
            else if (s.EndsWith("NEGATIVE")) { rhPositive = false; s = s[..^8]; }
            else if (s.EndsWith("POS")) { rhPositive = true; s = s[..^3]; }
            else if (s.EndsWith("NEG")) { rhPositive = false; s = s[..^3]; }
            else return null;

            return s is "A" or "B" or "AB" or "O" ? new Group(s, rhPositive) : null;
        }

        /// <summary>
        /// Null when the bag may be given to the patient, otherwise a message for the user.
        /// WHOLE blood must be ABO-identical (it carries plasma too); PRBC follows the red-cell table;
        /// FFP, CRYO and PLATELET follow the plasma table (the donor's antibodies must not meet the
        /// recipient's red cells). Rh only matters for red-cell products.
        /// </summary>
        public static string? CheckBagForPatient(string component, string? bagGroupRaw, string? patientGroupRaw)
        {
            var bag = Parse(bagGroupRaw);
            if (bag == null) return "The blood group on this bag is not valid. It cannot be transfused.";

            var patient = Parse(patientGroupRaw);
            if (patient == null)
                return "The patient's blood group is not recorded (or is 'Unknown'). Record a confirmed ABO/Rh group before transfusing.";

            var comp = (component ?? string.Empty).Trim().ToUpperInvariant();
            var label = $"Bag group {Describe(bag.Value)} is not compatible with the patient's group {Describe(patient.Value)}";

            switch (comp)
            {
                case IpdConstants.BloodComponent.Whole:
                    if (bag.Value.Abo != patient.Value.Abo) return label + " (whole blood must be the same ABO group).";
                    return RhOk(bag.Value, patient.Value) ? null : label + " (Rh-positive blood for an Rh-negative patient).";

                case IpdConstants.BloodComponent.Prbc:
                    if (!RedCellAboOk(bag.Value.Abo, patient.Value.Abo)) return label + ".";
                    return RhOk(bag.Value, patient.Value) ? null : label + " (Rh-positive cells for an Rh-negative patient).";

                case IpdConstants.BloodComponent.Ffp:
                case IpdConstants.BloodComponent.Cryo:
                case IpdConstants.BloodComponent.Platelet:
                    return PlasmaAboOk(bag.Value.Abo, patient.Value.Abo) ? null : label + ".";

                default:
                    return "Unknown blood component. It cannot be transfused.";
            }
        }

        private static bool RhOk(Group bag, Group patient) => !bag.RhPositive || patient.RhPositive;

        // Donor red cells -> recipient: O gives to all, A to A/AB, B to B/AB, AB to AB.
        private static bool RedCellAboOk(string donor, string recipient) => donor switch
        {
            "O" => true,
            "A" => recipient is "A" or "AB",
            "B" => recipient is "B" or "AB",
            "AB" => recipient == "AB",
            _ => false,
        };

        // Donor plasma -> recipient: AB gives to all, A to A/O, B to B/O, O to O.
        private static bool PlasmaAboOk(string donor, string recipient) => donor switch
        {
            "AB" => true,
            "A" => recipient is "A" or "O",
            "B" => recipient is "B" or "O",
            "O" => recipient == "O",
            _ => false,
        };

        private static string Describe(Group g) => g.Abo + (g.RhPositive ? "+" : "-");
    }
}
