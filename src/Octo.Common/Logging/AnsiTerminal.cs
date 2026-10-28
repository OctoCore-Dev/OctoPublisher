namespace Octo.Common.Logging;

public static partial class AnsiTerminal
{
    private static readonly bool t_isAnsiSupported = !Console.IsOutputRedirected;

    [GeneratedRegex(@"\u001b\[[0-9;]*m")]
    private static partial Regex AnsiEscapeRegex();

    public static string StripAnsi(string text) => AnsiEscapeRegex().Replace(text, "");

    public const string Reset = "\u001b[0m";
    public const string Bold = "\u001b[1m";
    public const string Dim = "\u001b[2m";

    public const string Accent = "\u001b[38;2;96;205;255m";
    public const string Muted = "\u001b[38;2;140;140;140m";
    public const string Subtle = "\u001b[38;2;80;80;80m";
    public const string White = "\u001b[38;2;240;240;240m";
    public const string Green = "\u001b[38;2;108;203;95m";
    public const string Yellow = "\u001b[38;2;252;225;0m";
    public const string Red = "\u001b[38;2;255;99;71m";

    public static void Info(string message) => WriteLine($"{Muted}[INFO]{Reset} {White}{message}{Reset}");

    public static void Success(string message) => WriteLine($"{Green}[OK]{Reset} {White}{message}{Reset}");

    public static void Warning(string message) => WriteLine($"{Yellow}[WARN]{Reset} {White}{message}{Reset}");

    public static void Error(string message) => WriteLine($"{Red}[ERR]{Reset} {White}{message}{Reset}");

    public static void Step(string step, string message) => WriteLine($"{Accent}[*]{Reset} {Bold}{White}{step}{Reset} {Muted}-->{Reset} {message}");

    public static void Detail(string key, string value) => WriteLine($"    {Muted}|--{Reset} {Muted}{key}:{Reset} {Accent}{value}{Reset}");

    public static void Header(string title)
    {
        WriteLine();
        WriteLine($"{Accent}{Bold}=== {title.ToUpperInvariant()} ==={Reset}");
        WriteLine();
    }

    public static void ProgressBar(long current, long total, double bytesPerSec, string unit = "MB")
    {
        var percentage = total > 0 ? (double)current / total : 0.0;
        var barWidth = 28;
        var filledWidth = (int)(percentage * barWidth);
        var emptyWidth = barWidth - filledWidth;

        var bar = new string('=', Math.Max(0, filledWidth)) + (filledWidth < barWidth ? ">" : "") + new string(' ', Math.Max(0, emptyWidth - (filledWidth < barWidth ? 1 : 0)));

        var currentMb = current / 1048576.0;
        var totalMb = total / 1048576.0;
        var speedMb = bytesPerSec / 1048576.0;

        var text = $"\r    {Accent}[{bar}]{Reset} {White}{percentage * 100:0.0}%{Reset} {Muted}({currentMb:0.0} / {totalMb:0.0} {unit}, {speedMb:0.0} {unit}/s){Reset}    ";
        Console.Write(text);
    }

    public static void EndProgressBar() => Console.WriteLine();

    private static void WriteLine(string text = "")
    {
        if (t_isAnsiSupported)
        {
            Console.WriteLine(text);
        }
        else
        {
            var clean = AnsiEscapeRegex().Replace(text, "");
            Console.WriteLine(clean);
        }
    }
}
