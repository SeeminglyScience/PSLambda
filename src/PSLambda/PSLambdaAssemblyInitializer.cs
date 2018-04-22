using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSLambda
{
    /// <summary>
    /// Provides initialization services when the module is imported.
    /// </summary>
    public class PSLambdaAssemblyInitializer : IModuleAssemblyInitializer
    {
        private const string TypeAcceleratorTypeName = "System.Management.Automation.TypeAccelerators";

        private const string GetPropertyName = "Get";

        /// <summary>
        /// Attempts to create the type accelerator for <see cref="PSDelegate" />
        /// </summary>
        public void OnImport()
        {
            var accelType = typeof(PSObject).Assembly.GetType(TypeAcceleratorTypeName);
            if (accelType == null)
            {
                return;
            }

            var getProperty = accelType.GetProperty(GetPropertyName);
            if (getProperty == null)
            {
                return;
            }

            var existing = getProperty.GetValue(null) as Dictionary<string, Type>;
            if (existing == null)
            {
                return;
            }

            if (existing.ContainsKey(Strings.PSDelegateTypeAcceleratorName))
            {
                return;
            }

            var addMethod = accelType.GetMethod(
                Strings.AddMethodName,
                new[] { typeof(string), typeof(Type) });
            if (addMethod == null)
            {
                return;
            }

            try
            {
                addMethod.Invoke(
                    null,
                    new object[] { Strings.PSDelegateTypeAcceleratorName, typeof(PSDelegate) });
            }
            catch (Exception)
            {
                return;
            }
        }
    }
}
