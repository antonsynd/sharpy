namespace Sharpy
{
    /// <summary>
    /// Holds the comments associated with a single key (in a mapping) or item (in a
    /// sequence) for YAML roundtrip preservation, mirroring ruamel.yaml's comment model.
    /// </summary>
    /// <remarks>
    /// A node may have a comment on the line(s) preceding it (<see cref="BeforeComment"/>),
    /// a comment trailing it on the same line (<see cref="InlineComment"/>), or a comment
    /// on the line(s) following it (<see cref="AfterComment"/>). Comment text is stored
    /// verbatim, without the leading <c>#</c> marker.
    /// </remarks>
    [SharpyModuleType("yaml")]
    public class CommentInfo
    {
        /// <summary>Comment appearing on the line(s) before the associated node.</summary>
        public string? BeforeComment { get; set; }

        /// <summary>Comment trailing the associated node on the same line.</summary>
        public string? InlineComment { get; set; }

        /// <summary>Comment appearing on the line(s) after the associated node.</summary>
        public string? AfterComment { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance carries any comment text.
        /// </summary>
        public bool HasComments =>
            BeforeComment != null || InlineComment != null || AfterComment != null;
    }
}
