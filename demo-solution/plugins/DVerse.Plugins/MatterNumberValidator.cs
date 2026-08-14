using System;
using System.Text.RegularExpressions;
using Microsoft.Xrm.Sdk;

namespace DVerse.Plugins
{
    /// <summary>
    /// Validates dv_matternumber on dv_matter.
    ///
    /// Registration (conceptual only; this slice owns the imperative half,
    /// not the SdkMessageProcessingStep YAML, which lands in wave 4.4b):
    ///   Entity  : dv_matter
    ///   Message : Create, Update
    ///
    /// Rule: when dv_matternumber is present on the Target, its value must
    /// match ^M-\d{4,}$, an "M-" prefix followed by four or more digits
    /// (example: M-1234). A non-matching value throws
    /// InvalidPluginExecutionException naming the offending value and the
    /// expected format. dv_matternumber is optional on the schema
    /// (RequiredLevel none), so an absent attribute is not an error; this
    /// plugin only validates what is actually being set on this operation,
    /// which also means a Create or Update of dv_matter that does not touch
    /// dv_matternumber is left alone.
    ///
    /// Reads only the target entity from InputParameters. No Retrieve, no
    /// Web API call, no other external I/O, per the plug-in best practices
    /// the house style documents (BP-2 InvalidPluginExecutionException for
    /// user-facing errors; BP-3 trace at entry, exit, and every branch
    /// decision; BP-1 stateless, no instance fields carrying service or
    /// context state).
    /// </summary>
    public class MatterNumberValidator : IPlugin
    {
        private const string TargetEntityLogicalName = "dv_matter";
        private const string AttributeLogicalName = "dv_matternumber";
        private const string ExpectedFormatDescription =
            "an \"M-\" prefix followed by four or more digits, for example M-1234";

        private static readonly Regex MatterNumberPattern =
            new Regex(@"^M-\d{4,}$", RegexOptions.Compiled);

        public void Execute(IServiceProvider serviceProvider)
        {
            // Declared outside the try so it is reachable from the catch
            // block for tracing, same as the house pattern.
            ITracingService tracingService = null;

            try
            {
                var context = (IPluginExecutionContext)serviceProvider
                    .GetService(typeof(IPluginExecutionContext));
                tracingService = (ITracingService)serviceProvider
                    .GetService(typeof(ITracingService));

                if (context.PrimaryEntityName != TargetEntityLogicalName)
                {
                    tracingService?.Trace(
                        "MatterNumberValidator: primary entity is '{0}', not '{1}'; no-op.",
                        context.PrimaryEntityName, TargetEntityLogicalName);
                    return;
                }

                if (!context.InputParameters.Contains("Target") ||
                    !(context.InputParameters["Target"] is Entity target))
                {
                    tracingService?.Trace(
                        "MatterNumberValidator: Target not present in InputParameters or not "
                        + "an Entity (for example, a Delete's EntityReference); no-op.");
                    return;
                }

                if (!target.Contains(AttributeLogicalName))
                {
                    tracingService?.Trace(
                        "MatterNumberValidator: '{0}' not present on Target; nothing to "
                        + "validate for dv_matter Id={1}.", AttributeLogicalName, target.Id);
                    return;
                }

                var matterNumber = target.GetAttributeValue<string>(AttributeLogicalName);

                tracingService?.Trace(
                    "MatterNumberValidator: validating {0}='{1}' for dv_matter Id={2}.",
                    AttributeLogicalName, matterNumber, target.Id);

                if (matterNumber == null || !MatterNumberPattern.IsMatch(matterNumber))
                {
                    throw new InvalidPluginExecutionException(
                        $"Matter number '{matterNumber}' is not valid. Expected format: "
                        + $"{ExpectedFormatDescription}.");
                }

                tracingService?.Trace(
                    "MatterNumberValidator: '{0}' is a valid matter number.", matterNumber);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracingService?.Trace(
                    "[MatterNumberValidator] Unhandled {0}: {1}", ex.GetType().Name, ex.Message);

                throw new InvalidPluginExecutionException(
                    $"An unexpected error occurred in MatterNumberValidator: {ex.Message}", ex);
            }
        }
    }
}
