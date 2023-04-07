// The custom comment parser of our Visual Studio extension does not attempt
// to figure out whether a specific text span is in a comment, or a string, or is
// actually code. Doing so in general is pretty complicated. Instead, we use
// a hack and rely on the default Visual Studio formatter. Unfortunately, that
// means we cannot write automated tests because it would require a running
// Visual Studio instance. 
// Despite using the Visual Studio formatter, some non-trivial logic is necessary
// to identify the type of the comment ("//", "///", "/*", etc). This file contains
// various combinations of those. The idea is to define ENABLE_COMMENT_TYPE_DEBUGGING
// in the CommentClassifier.cs source file, in which case the doxygen highlighting
// is disabled and instead each comment type is highlighted differently. This makes
// debugging and testing easier.



int Expected_OneColorPerLine;
/// TripleSlash
//! DoubleSlashExclamation
// DoubleSlash
/** SlashStarStar */
/*! SlashStarExclamation */
/* SlashStar */
 

int Expected_OneColorPerLine;
  /// TripleSlash
  //! DoubleSlashExclamation
  // DoubleSlash
  /** SlashStarStar */
  /*! SlashStarExclamation */
  /* SlashStar */
 

int Expected_AllItalicGreen;
/**1f
  /*
44*/


int Expected_GrayBackground_IncludingForTheSpacesOnTheMiddleLine;
/*
   
*/

int Expected_GrayBackgroundForStartAndEnd_NothingOnTheMiddleLine;
/*

*/


int Expected_FirstBlack_ThenGrayBackground_ThenBlack_ThenGrayBackground;
// fooY
/**/
// fooX
/**/


int Expected_AllOrange;
/*!
// fooX
/**/


int Expected_AllOrange;
/*!
// fooX
*/


int Expected_AllBlueTillXX;
//! bla \
	some stuff\
\
 \
	this here should still be blue. XX


int Expected_2LinesBlue_Then3LinesGrayBackground;
//!\
	asda
/*
\
/***/


int Expected_AllLinesBlue;
//! foo \
/// asdasd \
// asdasd


int Expected_GrayThenBlackInFirstLine_TwiceGrayInSecondLine;
/**/// /**//**/ AAAA
/**//**/


int Expected_FirstYellow_ThenGray_ThenItalicGreen;
/*!*//**//***/

int Expected_FirstLineGreen_SecondLineBlue;
///*!*//**//***/
//!/*!*//**//***/


int Expected_OneColorPerLine;
/***/
/**/
/*!*/
// test **foo** test
/// test **foo** test
/* test **foo** test */
/*! test **foo** test */


int Expected_FirstTwoLinesBlack_OneLineBlue;
// foo \
	*/
//! bla


int Expected_FirstThreeLinesBlack_OneLineBlue;
/*
// foo \
	*/
//! bla
	
