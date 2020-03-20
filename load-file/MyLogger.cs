using System;
using Logging;
using static load_file.Globals;

namespace load_file
{
    /// <summary>
    /// Examines the settings and if the application is running as a console application, sends messages to the console,
    /// otherwise sends messages to the logging store indicated by the AppSettingsImpl. Extends the base class Logger
    /// </summary>

    class MyLogger : Logger
    {
        /// <summary>
        /// Initialize logging level based on AppSettingsImpl. If the optional logging level is not specified then the default
        /// is Informational, which logs everything
        /// </summary>

        public void InitLoggingSettings()
        {
            Level = LogLevel.Information;

            if (AppSettingsImpl.LogLevel.Initialized)
            {
                switch (AppSettingsImpl.LogLevel.Value.ToString().ToLower())
                {
                    case "err":
                        Level = LogLevel.Error;
                        break;
                    case "warn":
                        Level = LogLevel.Warning;
                        break;
                }
            }

            Output = LogOutput.ToConsole; // output to console unless otherwise specified

            if (AppSettingsImpl.Log.Initialized)
            {
                switch (AppSettingsImpl.Log.Value.ToString().ToLower())
                {
                    // since Console is default, only take action if that is changed by the user
                    case "file":
                        Output = LogOutput.ToFile;
                        break;
                    case "db":
                        Output = LogOutput.ToDatabase;
                        break;
                }
            }
            if (AppSettingsImpl.JobID.Initialized)
            {
                // The settings parser ensures that the GUID is valid so this is safe
                GUID = Guid.Parse(AppSettingsImpl.JobID.Value);
            }
            JobName = "load-file";
        }

        /// <summary>
        /// Invokes the base class constructor
        /// </summary>

        public MyLogger() : base() { }
    }
}
