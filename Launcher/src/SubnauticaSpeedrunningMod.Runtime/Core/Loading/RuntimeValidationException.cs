using System;
using SubnauticaSpeedrunningMod.Shared;

namespace SubnauticaSpeedrunningMod.Runtime
{
    internal sealed class RuntimeValidationException : Exception
    {
        public RuntimeValidationException(GameInstallValidationReport validationReport)
            : base(validationReport.ToUserFacingMessage())
        {
            ValidationReport = validationReport;
        }

        public GameInstallValidationReport ValidationReport { get; private set; }
    }
}
