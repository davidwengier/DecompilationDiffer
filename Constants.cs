namespace DecompilationDiffer;

public static class Constants
{
    public const string InitialCode = """
        using System.Collections.Generic;
        using System.Linq;

        class C
        {
            private IEnumerable<int> _data = M(new [] {1, 2, 3});

            static IEnumerable<int> M(IEnumerable<int> input)
            {
                return input.Select(x => x);
            }
        }
        """;

    public const string Version1Code = """
        using System.Collections.Generic;
        using System.Linq;

        class C
        {
            private IEnumerable<int> _data = M([1, 2, 3]);

            static IEnumerable<int> M(IEnumerable<int> input)
            {
                return input.Select(x => x);
            }
        }
        """;

    public const string Version2Code = """
        using System.Collections.Generic;
        using System.Linq;

        class C
        {
            private IEnumerable<int> _data = M(1, 2, 3);

            static IEnumerable<int> M(params IEnumerable<int> input)
            {
                return input.Select(x => x);
            }
        }
        """;
}
