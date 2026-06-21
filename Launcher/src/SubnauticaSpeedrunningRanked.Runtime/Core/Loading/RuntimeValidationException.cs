using System;
using SubnauticaSpeedrunningRanked.Shared;

namespace SubnauticaSpeedrunningRanked.Runtime
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
