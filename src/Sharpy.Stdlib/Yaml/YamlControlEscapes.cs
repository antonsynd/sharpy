namespace Sharpy
{
    /// <summary>
    /// Post-processes YamlDotNet emitter output so that raw control bytes inside
    /// double-quoted scalars are spelled as YAML escape sequences, matching PyYAML.
    /// </summary>
    /// <remarks>
    /// YamlDotNet 18.x escapes every C0 control except horizontal tab (0x09). The style
    /// authority forces double-quoting for strings containing a tab, and the emitter never
    /// uses tabs for formatting (spaces only), so a global replacement is safe (#1598).
    /// </remarks>
    internal static class YamlControlEscapes
    {
        internal static string EscapeRawControls(string yaml)
        {
            if (yaml.IndexOf('\t') < 0)
                return yaml;

            return yaml.Replace("\t", "\\t");
        }
    }
}
