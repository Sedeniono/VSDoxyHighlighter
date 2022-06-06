using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VSDoxyHighlighter
{
  /// <summary>
  /// Known types of formats. The integer values are used as indices into arrays.
  /// </summary>
  public enum FormatTypes : uint
  {
    NormalKeyword,
    Warning,
    Note,
    Parameter
  }


  // Represents the format of a single continuous fragment of text.
  [DebuggerDisplay("StartIndex={StartIndex}, Length={Length}, Type={Type}")]
  public struct FormattedFragment
  {
    /// <summary>
    /// Index where the formatting should start.
    /// </summary>
    public int StartIndex { get; private set; }

    /// <summary>
    /// How many characters to format.
    /// </summary>
    public int Length { get; private set; }

    /// <summary>
    /// The text fragment should be formatted according to this type.
    /// </summary>
    public FormatTypes Type { get; private set; }

    public FormattedFragment(int startIndex, int length, FormatTypes type)
    {
      Debug.Assert(startIndex >= 0);
      Debug.Assert(length >= 0);

      StartIndex = startIndex;
      Length = length;
      Type = type;
    }

    public override bool Equals(object obj)
    {
      if (!(obj is FormattedFragment casted)) {
        return false;
      }

      return
        StartIndex == casted.StartIndex
        && Length == casted.Length
        && Type == casted.Type;
    }

    public override int GetHashCode()
    {
      return Tuple.Create(StartIndex, Length, Type).GetHashCode();
    }
  }


  /// <summary>
  /// Provides facilities to format the doxygen comments in a piece of source code.
  /// </summary>
  public class CommentFormatter
  {
    public CommentFormatter()
    {
      mNoParamMatchers = new List<Tuple<Regex, FormatTypes>>();
      mNoParamMatchers.Add(Tuple.Create(
        new Regex(@"(?:^|\/\*|\/\/\/)[ \t]*\**[ \t]*\B((?:@|\\)(?:brief|details|see|return|returns|ingroup))\b", RegexOptions.Compiled | RegexOptions.Multiline),
        FormatTypes.NormalKeyword));
      mNoParamMatchers.Add(Tuple.Create(
        new Regex(@"(?:^|\/\*|\/\/\/)[ \t]*\**[ \t]*\B((?:@|\\)(?:warning))\b", RegexOptions.Compiled | RegexOptions.Multiline),
        FormatTypes.Warning));
      mNoParamMatchers.Add(Tuple.Create(
        new Regex(@"(?:^|\/\*|\/\/\/)[ \t]*\**[ \t]*\B((?:@|\\)(?:note|todo))\b", RegexOptions.Compiled | RegexOptions.Multiline),
        FormatTypes.Note));

      mOneParamMatchers = new List<Tuple<Regex, FormatTypes /*keyword*/, FormatTypes /*param*/>>();
      mOneParamMatchers.Add(Tuple.Create(
        new Regex(@"(?:^|\/\*|\/\/\/)[ \t]*\**[ \t]*\B((?:@|\\)(?:param|tparam|param\[in\]|param\[out\]|throw|throws|exception|p|ref|defgroup))[ \t]+(\w+)",
          RegexOptions.Compiled | RegexOptions.Multiline),
        FormatTypes.NormalKeyword, FormatTypes.Parameter));
      mOneParamMatchers.Add(Tuple.Create(
        new Regex(@"\B((?:@|\\)(?:p|c|ref))[ \t]+(\w+)", RegexOptions.Compiled),
        FormatTypes.NormalKeyword, FormatTypes.Parameter));
    }


    /// <summary>
    /// Computes the way whole the provided text should be formatted.
    /// </summary>
    /// <param name="text">This whole text is formatted.</param>
    /// <returns>A list of fragments that point into the given "text" and which should be formatted.</returns>
    public List<FormattedFragment> FormatText(string text)
    {
      var result = new List<FormattedFragment>();

      foreach (var matcher in mNoParamMatchers) {
        Regex regexMatcher = matcher.Item1;
        FormatTypes formatType = matcher.Item2;

        var foundMatches = regexMatcher.Matches(text);
        foreach (Match m in foundMatches) {
          if (m.Groups.Count == 2) {
            Group group = m.Groups[1];
            result.Add(new FormattedFragment(group.Index, group.Length, formatType));
          }
        }
      }

      foreach (var matcher in mOneParamMatchers) {
        Regex regexMatcher = matcher.Item1;
        FormatTypes keywordType = matcher.Item2;
        FormatTypes paramType = matcher.Item3;

        var foundMatches = regexMatcher.Matches(text);
        foreach (Match m in foundMatches) {
          if (m.Groups.Count == 3) {
            Group keywordGroup = m.Groups[1];
            Group paramGroup = m.Groups[2];

            result.Add(new FormattedFragment(keywordGroup.Index, keywordGroup.Length, keywordType));
            result.Add(new FormattedFragment(paramGroup.Index, paramGroup.Length, paramType));
          }
        }
      }

      return result;
    }


    private readonly List<Tuple<Regex, FormatTypes>> mNoParamMatchers;
    private readonly List<Tuple<Regex, FormatTypes /*keyword*/, FormatTypes /*param*/>> mOneParamMatchers;
  }
}
