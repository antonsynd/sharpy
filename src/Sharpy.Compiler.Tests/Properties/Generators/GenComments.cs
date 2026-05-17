using CsCheck;

namespace Sharpy.Compiler.Tests.Properties.Generators;

internal static class GenComments
{
    private static readonly string[] LeadingSources =
    {
        "# TODO: fix this\nx = 1\n",
        "# important note\ny = x + 1\n",
        "# this is a comment\nprint(x)\n",
        "# see above\nresult = compute()\n",
        "# type: int\npass\n",
        "# returns nothing\nx = 1\n",
        "# helper function\ny = x + 1\n",
        "# loop invariant holds\nprint(x)\n",
        "# edge case\nresult = compute()\n",
        "# validated input\npass\n",
        "# αβγ unicode\nx = 1\n",
        "# special chars !@$%^&*()\ny = x + 1\n",
        "#\nx = 1\n",
        "#  trailing spaces  \nresult = compute()\n",
        "# nested hash # inside\npass\n"
    };

    private static readonly string[] TrailingSources =
    {
        "x = 1  # TODO: fix this\n",
        "y = x + 1  # important note\n",
        "print(x)  # this is a comment\n",
        "result = compute()  # see above\n",
        "pass  # type: int\n",
        "x = 1  # returns nothing\n",
        "y = x + 1  # helper function\n",
        "print(x)  # loop invariant\n",
        "result = compute()  # edge case\n",
        "pass  # validated input\n",
        "x = 1  # αβγ unicode\n",
        "y = x + 1  # special chars !@$%^&*()\n",
        "x = 1  #\n",
        "pass  #  trailing spaces  \n"
    };

    private static readonly string[] MultiCommentSources =
    {
        "# first\n# second\nx = 1\n",
        "# line one\n# line two\ny = x + 1\n",
        "# above\n# also above\nprint(x)\n",
        "# comment a\n# comment b\nresult = compute()\n",
        "# top\n# middle\npass\n",
        "# header\n# subheader\nx = 1\n"
    };

    private static readonly string[] NestedSources =
    {
        "# outer comment\nclass Foo:\n    # inner comment\n    def bar(self):\n        pass\n",
        "# module doc\nclass Baz:\n    # class doc\n    def qux(self):\n        pass\n",
        "# top level\ndef foo():\n    # inside function\n    pass\n",
        "# first\nclass A:\n    # method doc\n    def m(self):\n        # body comment\n        pass\n"
    };

    private static readonly string[] MixedSources =
    {
        "# above\nx = 1  # inline\n",
        "# leading\ny = x + 1  # trailing\n",
        "# doc\nprint(x)  # side note\n",
        "# header\nresult = compute()  # trace\n",
        "# top\npass  # noop\n"
    };

    private static readonly string[] CommentTexts =
    {
        "# TODO: fix this", "# important note", "# this is a comment",
        "# see above", "# type: int", "# returns nothing",
        "# helper function", "# loop invariant holds", "# edge case",
        "# validated input", "# αβγ unicode", "# special chars !@$%^&*()",
        "#", "#  trailing spaces  ", "# nested hash # inside"
    };

    public static Gen<string> SourceWithLeadingComment { get; } =
        Gen.OneOfConst(LeadingSources);

    public static Gen<string> SourceWithTrailingComment { get; } =
        Gen.OneOfConst(TrailingSources);

    public static Gen<string> SourceWithMultipleComments { get; } =
        Gen.OneOfConst(MultiCommentSources);

    public static Gen<string> SourceWithNestedComments { get; } =
        Gen.OneOfConst(NestedSources);

    public static Gen<string> SourceWithMixedComments { get; } =
        Gen.OneOfConst(MixedSources);

    public static Gen<string> CommentText { get; } =
        Gen.OneOfConst(CommentTexts);
}
