using Microsoft.Extensions.Configuration;
using NLog.Config;

namespace eDocGenEngine
{
    internal class NLogLoggingConfiguration : LoggingConfiguration
    {
        private IConfigurationSection configurationSection;

        public NLogLoggingConfiguration(IConfigurationSection configurationSection)
        {
            this.configurationSection = configurationSection;
        }
    }
}