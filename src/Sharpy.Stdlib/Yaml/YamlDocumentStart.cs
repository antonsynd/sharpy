using System;

namespace Sharpy
{
    /// <summary>
    /// The single authority on suppressing YamlDotNet's spurious explicit document-start marker
    /// (#1467) — the <c>---</c> that opens a dumped document which needs no opening.
    ///
    /// <para>
    /// The cause is in the emitter, not in Sharpy: <c>YamlDotNet.Core.Emitter</c> consults
    /// <c>CheckEmptyDocument</c> when deciding whether the document start may stay implicit, and
    /// that predicate answers "not empty, write the marker" for a root node whose single scalar
    /// has an EMPTY VALUE. The decision is made from the parsing-event stream, before any style
    /// choice is rendered, so no combination of <c>ScalarStyle</c> or <c>EmitterSettings</c>
    /// avoids it — which is why this is compensation after the fact rather than configuration.
    /// PyYAML 6.0.3 writes <c>''\n</c>; Sharpy wrote <c>--- ''\n</c>.
    /// </para>
    ///
    /// <para>
    /// A shared authority rather than a fix at one dump site, for the reason
    /// <see cref="YamlDocumentEnd"/> is one (#1145): <c>safe_dump</c> builds a YamlDotNet
    /// <c>ISerializer</c> while <c>roundtrip_dump</c> drives the raw <c>Emitter</c>, so BOTH meet
    /// this predicate on their own account and both were emitting the marker. Fixing one would
    /// have left the two surfaces disagreeing about the same byte — a divergence Sharpy would
    /// have invented, not inherited.
    /// </para>
    ///
    /// <para>
    /// Scope note: the OTHER half of #1467, a null document emitted as no value at all
    /// (<c>--- \n</c>, which a conforming parser reads as an EMPTY document), is not fixed here.
    /// It is fixed by spelling null as the plain scalar <c>null</c> at the point of emission,
    /// which is a better fix than suppressing a marker around an absent value — and it makes this
    /// class's job the single remaining case, the empty string.
    /// </para>
    /// </summary>
    internal static class YamlDocumentStart
    {
        private const string SpuriousMarker = "--- ";

        /// <summary>
        /// Removes the leading document-start marker when <paramref name="document"/> is the one
        /// value that provokes it.
        /// </summary>
        /// <param name="emitted">The emitted document text, exactly as written.</param>
        /// <param name="document">
        /// The value that was emitted. Keyed on the VALUE rather than on the text so that a
        /// document which legitimately begins with <c>---</c> is never touched: after the null
        /// case is spelled explicitly, the root empty string is the only value for which
        /// <c>CheckEmptyDocument</c> can fire, because it inspects the ROOT node — an empty string
        /// nested inside a mapping or sequence does not reach it.
        /// </param>
        internal static string Suppress(string emitted, object? document)
        {
            if (!(document is string text) || text.Length != 0)
            {
                return emitted;
            }

            return emitted.StartsWith(SpuriousMarker, StringComparison.Ordinal)
                ? emitted.Substring(SpuriousMarker.Length)
                : emitted;
        }
    }
}
