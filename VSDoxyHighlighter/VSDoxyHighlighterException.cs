using System;

namespace VSDoxyHighlighter
{
  /// <summary>
  /// We use a custom exception class to throw generic exceptions, because in case the exception
  /// propagates till Visual Studio itself catches it, the exception name appears in the info bar 
  /// message of Visual Studio. Thus, it is immediately clear that our extension caused the exception.
  /// </summary>
  internal class VSDoxyHighlighterException : Exception
  {
    public VSDoxyHighlighterException(string message) : base(message) 
    { 
    } 
  }
}
