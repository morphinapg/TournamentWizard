using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace TournamentWizard.ViewModels
{
    [DataContract]
    internal static class FeatureSwitches
    {
        /// <summary>
        /// The configuration property name for <see cref="EnableINotifyPropertyChangingSupport"/>.
        /// </summary>
        private const string EnableINotifyPropertyChangingSupportPropertyName = "MVVMTOOLKIT_ENABLE_INOTIFYPROPERTYCHANGING_SUPPORT";

        /// <summary>
        /// The backing field for <see cref="EnableINotifyPropertyChangingSupport"/>.
        /// </summary>
        private static int enableINotifyPropertyChangingSupport;

        /// <summary>
        /// Gets a value indicating whether or not support for <see cref="System.ComponentModel.INotifyPropertyChanging"/> should be enabled (defaults to <see langword="true"/>).
        /// </summary>
        public static bool EnableINotifyPropertyChangingSupport
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetConfigurationValue(EnableINotifyPropertyChangingSupportPropertyName, ref enableINotifyPropertyChangingSupport, true);
        }

        /// <summary>
        /// Gets a configuration value for a specified property.
        /// </summary>
        /// <param name="propertyName">The property name to retrieve the value for.</param>
        /// <param name="cachedResult">The cached result for the target configuration value.</param>
        /// <param name="defaultValue">The default value for the feature switch, if not set.</param>
        /// <returns>The value of the specified configuration setting.</returns>
        private static bool GetConfigurationValue(string propertyName, ref int cachedResult, bool defaultValue)
        {
            // The cached switch value has 3 states:
            //   0: unknown.
            //   1: true
            //   -1: false
            //
            // This method doesn't need to worry about concurrent accesses to the cached result,
            // as even if the configuration value is retrieved twice, that'll always be the same.
            if (cachedResult < 0)
            {
                return false;
            }

            if (cachedResult > 0)
            {
                return true;
            }

            // Get the configuration switch value, or its default.
            // All feature switches have a default set in the .targets file.
            if (!AppContext.TryGetSwitch(propertyName, out bool isEnabled))
            {
                isEnabled = defaultValue;
            }

            // Update the cached result
            cachedResult = isEnabled ? 1 : -1;

            return isEnabled;
        }
    }
}
