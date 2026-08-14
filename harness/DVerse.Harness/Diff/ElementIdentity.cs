namespace DVerse.Harness.Diff;

/// <summary>
/// One element's computed identity: which ratified class family it belongs
/// to (docs/design/element-identity-model.md section 3, for example
/// "FormXmlControl" or "RootComponentsEntry"), the key string that
/// correlated it across the two document versions being matched, and
/// whether that key is a trustworthy platform-guaranteed identifier or a
/// fallback the diff-aware gates (7.3) must treat with suspicion.
/// <para>
/// <see cref="IsWarning"/> is true for exactly the cases the ratified model
/// names as honest fallbacks (section 4): a positional FormXml column/row
/// match, or a match made against a control class or component type this
/// model has never surveyed. It is false for every GUID-keyed or
/// schema/logical-name-keyed match, which the model's clone-stability
/// result (section 1) empirically proved stable. This flag is the seam
/// 7.3's refusal logic reads: a warning here is what turns into a refusal
/// there, per mission ruling 4.
/// </para>
/// </summary>
public sealed record ElementIdentity(string ClassKind, string Key, bool IsWarning);
